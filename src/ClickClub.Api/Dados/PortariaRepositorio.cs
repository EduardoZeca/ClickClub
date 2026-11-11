using System.Data;
using ClickClub.Api.Contratos;
using ClickClub.Api.Seguranca;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class PortariaRepositorio
{
    private readonly string _connectionString;
    private readonly CarteirinhaAssinaturaServico _assinatura;

    public PortariaRepositorio(
        IConfiguration configuration, CarteirinhaAssinaturaServico assinatura)
    {
        _connectionString = configuration.GetConnectionString("ClickClub")
            ?? throw new InvalidOperationException("Conexão MySQL não configurada.");
        _assinatura = assinatura;
    }

    public async Task<List<UnidadePortariaResposta>> UnidadesAsync(
        long operadorId, bool administrador, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT u.id, u.nome, u.sigla
            FROM unidade u
            WHERE u.status='Ativa' AND (
                @administrador=TRUE OR EXISTS (
                    SELECT 1 FROM papel_usuario pu
                    WHERE pu.usuario_id=@operador AND pu.papel='Porteiro'
                      AND pu.unidade_id=u.id))
            ORDER BY u.nome, u.id
            """, conexao);
        comando.Parameters.AddWithValue("@operador", operadorId);
        comando.Parameters.AddWithValue("@administrador", administrador);

        var unidades = new List<UnidadePortariaResposta>();
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
            unidades.Add(new UnidadePortariaResposta(
                leitor.GetInt64(0), leitor.GetString(1),
                leitor.IsDBNull(2) ? null : leitor.GetString(2)));
        return unidades;
    }

    // Resultado null significa unidade fora do escopo do operador.
    // Toda decisão de entrada é apurada na API, junto à gravação do registro.
    public async Task<ValidacaoQrResposta?> ValidarAsync(
        ValidacaoQrEntrada entrada, long operadorId,
        bool administrador, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        var unidade = await BuscarUnidadeNoEscopoAsync(
            conexao, transacao, entrada.UnidadeId,
            operadorId, administrador, ct);
        if (unidade is null)
            return null;

        var agora = DateTime.UtcNow;
        CredencialConferida? credencial = null;
        long? reservaRegistrada = null;
        string motivo;

        if (unidade != "Ativa")
        {
            motivo = "UnidadeInativa";
        }
        else if (!_assinatura.TentarVerificarAssinatura(
                     entrada.CodigoQr, out var payload) || payload is null)
        {
            motivo = "QrInvalido";
        }
        else
        {
            credencial = await BuscarCredencialAsync(
                conexao, transacao, payload.IdentificadorPublico, ct);
            motivo = ConferirCredencial(credencial, payload, agora);

            if (motivo == "Permitido" && credencial is not null)
            {
                motivo = await ConferirInadimplenciaAsync(
                    conexao, transacao, credencial.AssociacaoId,
                    credencial.FusoOrigem, agora, ct);
            }
        }

        if (motivo == "Permitido" &&
            entrada.TipoAcesso == "UsoReserva" && credencial is not null)
        {
            (motivo, reservaRegistrada) = await ConferirReservaAsync(
                conexao, transacao, entrada.ReservaId!.Value,
                entrada.UnidadeId, credencial.AssociacaoId, agora, ct);
        }

        var permitido = motivo == "Permitido";
        using var registrar = new MySqlCommand("""
            INSERT INTO registro_acesso
                (unidade_id, pessoa_id, credencial_carteirinha_id,
                 reserva_id, porteiro_usuario_id, tipo, resultado,
                 motivo_codigo, data_hora_utc)
            VALUES (@unidade, @pessoa, @credencial, @reserva,
                    @operador, @tipo, @resultado, @motivo, @agora)
            """, conexao, transacao);
        registrar.Parameters.AddWithValue("@unidade", entrada.UnidadeId);
        registrar.Parameters.AddWithValue("@pessoa",
            credencial is null ? DBNull.Value : credencial.PessoaId);
        registrar.Parameters.AddWithValue("@credencial",
            credencial is null ? DBNull.Value : credencial.Id);
        registrar.Parameters.AddWithValue("@reserva",
            reservaRegistrada.HasValue ? reservaRegistrada.Value : DBNull.Value);
        registrar.Parameters.AddWithValue("@operador", operadorId);
        registrar.Parameters.AddWithValue("@tipo", entrada.TipoAcesso);
        registrar.Parameters.AddWithValue("@resultado", permitido ? "Permitido" : "Negado");
        registrar.Parameters.AddWithValue("@motivo", motivo);
        registrar.Parameters.AddWithValue("@agora", agora);
        await registrar.ExecuteNonQueryAsync(ct);
        var registroId = registrar.LastInsertedId;
        await transacao.CommitAsync(ct);

        return new ValidacaoQrResposta(
            registroId, permitido, permitido ? "Permitido" : "Negado",
            motivo, Mensagem(motivo), credencial?.NomePessoa, agora);
    }

    // Validação de convidado de reserva existente, sem emissão de carteirinha.
    // A identificação deve ser conferida presencialmente pelo porteiro.
    // Resultado null significa unidade fora do escopo da conta.
    public async Task<ValidacaoConvidadoResposta?> ValidarConvidadoAsync(
        ValidacaoConvidadoEntrada entrada, long operadorId,
        bool administrador, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        var unidade = await BuscarUnidadeNoEscopoAsync(
            conexao, transacao, entrada.UnidadeId,
            operadorId, administrador, ct);
        if (unidade is null)
            return null;

        var agora = DateTime.UtcNow;
        ConvidadoConferido? convidado = null;
        string motivo;

        if (unidade != "Ativa")
            motivo = "UnidadeInativa";
        else if (!entrada.IdentidadeConferida)
            motivo = "IdentidadeNaoConferida";
        else
        {
            convidado = await BuscarConvidadoAsync(
                conexao, transacao, entrada.UnidadeId,
                entrada.ReservaId, entrada.IdentificacaoMinima!.Trim(), ct);

            if (convidado is null)
                motivo = "ConvidadoNaoEncontrado";
            else if (convidado.Situacao != "Autorizado")
                motivo = "ConvidadoNaoAutorizado";
            else if (convidado.StatusAssociacao != "Ativa")
                motivo = "AssociacaoInativa";
            else if (convidado.StatusReserva != "Confirmada")
                motivo = "ReservaCancelada";
            else if (agora < convidado.InicioUtc || agora >= convidado.FimUtc)
                motivo = "ForaDoPeriodo";
            else if (convidado.ValorContratado > 0 &&
                     convidado.StatusCobranca is not
                         ("QuitadaSimulada" or "Transferida"))
                motivo = "TaxaPendente";
            else
                motivo = "Permitido";
        }

        var permitido = motivo == "Permitido";
        using var registrar = new MySqlCommand("""
            INSERT INTO registro_acesso
                (unidade_id, convidado_id, reserva_id,
                 porteiro_usuario_id, tipo, resultado,
                 motivo_codigo, data_hora_utc)
            VALUES (@unidade, @convidado, @reserva,
                    @operador, 'UsoReserva', @resultado, @motivo, @agora)
            """, conexao, transacao);
        registrar.Parameters.AddWithValue("@unidade", entrada.UnidadeId);
        registrar.Parameters.AddWithValue("@convidado",
            convidado is null ? DBNull.Value : convidado.Id);
        registrar.Parameters.AddWithValue("@reserva",
            convidado is null ? DBNull.Value : convidado.ReservaId);
        registrar.Parameters.AddWithValue("@operador", operadorId);
        registrar.Parameters.AddWithValue("@resultado", permitido ? "Permitido" : "Negado");
        registrar.Parameters.AddWithValue("@motivo", motivo);
        registrar.Parameters.AddWithValue("@agora", agora);
        await registrar.ExecuteNonQueryAsync(ct);
        var registroId = registrar.LastInsertedId;
        await transacao.CommitAsync(ct);

        return new ValidacaoConvidadoResposta(
            registroId, permitido, permitido ? "Permitido" : "Negado",
            motivo, Mensagem(motivo), convidado?.Nome, agora);
    }

    // Retorna null para unidade fora do escopo; não revela registros alheios.
    public async Task<List<RegistroPortariaResposta>?> HistoricoAsync(
        long unidadeId, long operadorId, bool administrador,
        int limite, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);

        if (await BuscarUnidadeNoEscopoAsync(
                conexao, null, unidadeId, operadorId, administrador, ct) is null)
            return null;

        using var comando = new MySqlCommand("""
            SELECT r.id, r.data_hora_utc, r.tipo,
                   r.resultado, r.motivo_codigo, COALESCE(p.nome, cr.nome, ce.nome)
            FROM registro_acesso r
            LEFT JOIN pessoa p ON p.id=r.pessoa_id
            LEFT JOIN convidado_reserva cr ON cr.id=r.convidado_id
            LEFT JOIN convidado_evento ce ON ce.id=r.convidado_evento_id
            WHERE r.unidade_id=@unidade
            ORDER BY r.data_hora_utc DESC, r.id DESC
            LIMIT @limite
            """, conexao);
        comando.Parameters.AddWithValue("@unidade", unidadeId);
        comando.Parameters.AddWithValue("@limite", Math.Clamp(limite, 1, 100));

        var lista = new List<RegistroPortariaResposta>();
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
            lista.Add(new RegistroPortariaResposta(
                leitor.GetInt64(0),
                DateTime.SpecifyKind(leitor.GetDateTime(1), DateTimeKind.Utc),
                leitor.GetString(2), leitor.GetString(3), leitor.GetString(4),
                leitor.IsDBNull(5) ? null : leitor.GetString(5)));
        return lista;
    }

    private static async Task<string?> BuscarUnidadeNoEscopoAsync(
        MySqlConnection conexao, MySqlTransaction? transacao,
        long unidadeId, long operadorId, bool administrador,
        CancellationToken ct)
    {
        using var comando = new MySqlCommand("""
            SELECT u.status FROM unidade u
            WHERE u.id=@unidade AND (
                @administrador=TRUE OR EXISTS (
                    SELECT 1 FROM papel_usuario pu
                    WHERE pu.usuario_id=@operador AND pu.papel='Porteiro'
                      AND pu.unidade_id=u.id))
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@unidade", unidadeId);
        comando.Parameters.AddWithValue("@operador", operadorId);
        comando.Parameters.AddWithValue("@administrador", administrador);
        return await comando.ExecuteScalarAsync(ct) as string;
    }

    private static async Task<CredencialConferida?> BuscarCredencialAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        string identificadorPublico, CancellationToken ct)
    {
        using var comando = new MySqlCommand("""
            SELECT c.id, c.pessoa_id, c.associacao_id, c.versao,
                   c.emitida_em_utc, c.valida_ate_utc, c.revogada_em_utc,
                   p.nome, p.status, us.ativo, a.status, origem.fuso_horario,
                   (a.titular_pessoa_id=c.pessoa_id OR EXISTS (
                       SELECT 1 FROM dependente_associacao d
                       WHERE d.associacao_id=a.id
                         AND d.dependente_pessoa_id=c.pessoa_id
                         AND d.ativo=TRUE)),
                   EXISTS (
                       SELECT 1 FROM papel_usuario pu
                       WHERE pu.usuario_id=us.id
                         AND pu.papel IN ('Titular','Dependente'))
            FROM credencial_carteirinha c
            JOIN pessoa p ON p.id=c.pessoa_id
            JOIN usuario us ON us.pessoa_id=p.id
            JOIN associacao a ON a.id=c.associacao_id
            JOIN unidade origem ON origem.id=a.unidade_origem_id
            WHERE c.identificador_publico=@identificador
            LIMIT 1
            FOR SHARE
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@identificador", identificadorPublico);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct))
            return null;

        return new CredencialConferida(
            leitor.GetInt64(0), leitor.GetInt64(1), leitor.GetInt64(2),
            leitor.GetInt32(3),
            DateTime.SpecifyKind(leitor.GetDateTime(4), DateTimeKind.Utc),
            DateTime.SpecifyKind(leitor.GetDateTime(5), DateTimeKind.Utc),
            leitor.IsDBNull(6) ? null : DateTime.SpecifyKind(
                leitor.GetDateTime(6), DateTimeKind.Utc),
            leitor.GetString(7), leitor.GetString(8), leitor.GetBoolean(9),
            leitor.GetString(10), leitor.GetString(11),
            Convert.ToInt32(leitor.GetValue(12)) == 1,
            Convert.ToInt32(leitor.GetValue(13)) == 1);
    }

    private static async Task<ConvidadoConferido?> BuscarConvidadoAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long unidadeId, long reservaId, string identificacao,
        CancellationToken ct)
    {
        using var comando = new MySqlCommand("""
            SELECT cr.id, cr.nome, cr.situacao, r.id, r.status,
                   r.inicio_utc, r.fim_utc, r.valor_contratado,
                   COALESCE(c.status,'Gratuita'), a.status
            FROM convidado_reserva cr
            JOIN reserva r ON r.id=cr.reserva_id
            JOIN espaco e ON e.id=r.espaco_id
            JOIN associacao a ON a.id=r.associacao_id
            LEFT JOIN cobranca_reserva c ON c.reserva_id=r.id
            WHERE r.id=@reserva AND e.unidade_id=@unidade
              AND r.tipo_espaco_contratado IN ('SalaoFestas','Churrasqueira')
              AND cr.identificacao_minima=@identificacao
            LIMIT 1
            FOR SHARE
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@unidade", unidadeId);
        comando.Parameters.AddWithValue("@reserva", reservaId);
        comando.Parameters.AddWithValue("@identificacao", identificacao);

        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct))
            return null;

        return new ConvidadoConferido(
            leitor.GetInt64(0), leitor.GetString(1), leitor.GetString(2),
            leitor.GetInt64(3), leitor.GetString(4),
            DateTime.SpecifyKind(leitor.GetDateTime(5), DateTimeKind.Utc),
            DateTime.SpecifyKind(leitor.GetDateTime(6), DateTimeKind.Utc),
            leitor.GetDecimal(7), leitor.GetString(8), leitor.GetString(9));
    }

    private static string ConferirCredencial(
        CredencialConferida? credencial,
        CarteirinhaPayload payload, DateTime agoraUtc)
    {
        if (credencial is null || credencial.Versao != payload.Versao ||
            new DateTimeOffset(credencial.EmitidaEmUtc).ToUnixTimeSeconds() !=
                payload.EmitidaEmUnix ||
            new DateTimeOffset(credencial.ValidaAteUtc).ToUnixTimeSeconds() !=
                payload.ValidaAteUnix)
            return "CredencialInvalida";

        if (credencial.RevogadaEmUtc is not null)
            return "CredencialRevogada";
        // A data no MySQL preserva microssegundos: só ela define o limite
        // exato de 24 horas. O instante assinado é conferido acima em segundos.
        if (credencial.ValidaAteUtc <= agoraUtc)
            return "QrExpirado";
        if (credencial.StatusPessoa != "Ativa" || !credencial.UsuarioAtivo ||
            !credencial.PapelAssociativoAtivo)
            return "PessoaInativa";
        if (credencial.StatusAssociacao != "Ativa" || !credencial.VinculoAtivo)
            return "AssociacaoInativa";

        return "Permitido";
    }

    private static async Task<string> ConferirInadimplenciaAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long associacaoId, string fusoOrigem,
        DateTime agoraUtc, CancellationToken ct)
    {
        DateTime dataOrigem;
        try
        {
            dataOrigem = RegraInadimplencia.DataLocal(agoraUtc, fusoOrigem);
        }
        catch (InvalidOperationException)
        {
            return "FusoInvalido";
        }

        // Mesma regra da confirmação de reservas: somente mensalidade
        // EmAberto com mais de dez dias, na data local do clube de origem.
        using var comando = new MySqlCommand("""
            SELECT EXISTS (SELECT 1 FROM fatura f
                WHERE f.associacao_id=@associacao
                  AND f.tipo='Mensalidade' AND f.status='EmAberto'
                  AND f.valor_total>0 AND f.data_vencimento<@limite)
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@associacao", associacaoId);
        comando.Parameters.AddWithValue("@limite",
            dataOrigem.AddDays(-RegraInadimplencia.DiasTolerancia));

        return Convert.ToInt32(await comando.ExecuteScalarAsync(ct)) == 1
            ? "MensalidadeInadimplente" : "Permitido";
    }

    private static async Task<(string Motivo, long? ReservaId)>
        ConferirReservaAsync(MySqlConnection conexao, MySqlTransaction transacao,
            long reservaId, long unidadeId, long associacaoId,
            DateTime agoraUtc, CancellationToken ct)
    {
        using var comando = new MySqlCommand("""
            SELECT r.id, r.status, r.inicio_utc, r.fim_utc,
                   r.valor_contratado, COALESCE(c.status,'Gratuita')
            FROM reserva r
            JOIN espaco e ON e.id=r.espaco_id
            LEFT JOIN cobranca_reserva c ON c.reserva_id=r.id
            WHERE r.id=@reserva AND r.associacao_id=@associacao
              AND e.unidade_id=@unidade
            LIMIT 1
            FOR SHARE
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@reserva", reservaId);
        comando.Parameters.AddWithValue("@associacao", associacaoId);
        comando.Parameters.AddWithValue("@unidade", unidadeId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct))
            return ("ReservaInvalida", null);

        var id = leitor.GetInt64(0);
        var status = leitor.GetString(1);
        var inicio = DateTime.SpecifyKind(leitor.GetDateTime(2), DateTimeKind.Utc);
        var fim = DateTime.SpecifyKind(leitor.GetDateTime(3), DateTimeKind.Utc);
        var valor = leitor.GetDecimal(4);
        var cobranca = leitor.GetString(5);

        if (status != "Confirmada")
            return ("ReservaCancelada", id);
        if (agoraUtc < inicio || agoraUtc >= fim)
            return ("ForaDoPeriodo", id);
        if (valor > 0 && cobranca is not ("QuitadaSimulada" or "Transferida"))
            return ("TaxaPendente", id);

        return ("Permitido", id);
    }

    private static string Mensagem(string codigo) => codigo switch
    {
        "Permitido" => "Entrada autorizada pela API.",
        "UnidadeInativa" => "Este clube está inativo.",
        "QrInvalido" => "QR Code inválido ou adulterado.",
        "CredencialInvalida" => "Credencial não reconhecida ou inconsistente.",
        "CredencialRevogada" => "Carteirinha revogada. Solicite nova emissão quando elegível.",
        "QrExpirado" => "QR Code expirado. É necessária renovação online.",
        "PessoaInativa" => "Pessoa ou conta sem situação ativa.",
        "AssociacaoInativa" => "Associação ou vínculo sem autorização de acesso.",
        "FusoInvalido" => "Não foi possível validar a data local do clube de origem.",
        "MensalidadeInadimplente" => "Mensalidade vencida há mais de 10 dias. Entrada bloqueada.",
        "ReservaInvalida" => "Reserva não localizada para esta associação e unidade.",
        "ReservaCancelada" => "Reserva cancelada. Uso do espaço não autorizado.",
        "ForaDoPeriodo" => "Uso do espaço fora do período confirmado.",
        "TaxaPendente" => "Taxa de locação ainda não quitada ou transferida.",
        "IdentidadeNaoConferida" => "Confira presencialmente a identificação do convidado.",
        "ConvidadoNaoEncontrado" => "Convidado não cadastrado para esta reserva e unidade.",
        "ConvidadoNaoAutorizado" => "Este convidado foi removido da lista autorizada.",
        _ => "Acesso negado pela API."
    };

    private sealed record ConvidadoConferido(
        long Id, string Nome, string Situacao, long ReservaId,
        string StatusReserva, DateTime InicioUtc, DateTime FimUtc,
        decimal ValorContratado, string StatusCobranca,
        string StatusAssociacao);

    private sealed record CredencialConferida(
        long Id, long PessoaId, long AssociacaoId, int Versao,
        DateTime EmitidaEmUtc, DateTime ValidaAteUtc,
        DateTime? RevogadaEmUtc, string NomePessoa,
        string StatusPessoa, bool UsuarioAtivo,
        string StatusAssociacao, string FusoOrigem,
        bool VinculoAtivo, bool PapelAssociativoAtivo);
}
