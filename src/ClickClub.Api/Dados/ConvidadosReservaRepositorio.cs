using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class ConvidadosReservaRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<ConvidadosReservaResposta?> ConsultarAsync(
        long reservaId, long usuarioId, bool titular, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);

        DateTime inicioUtc;
        string tipo, status;
        using (var reserva = new MySqlCommand("""
            SELECT r.inicio_utc, r.tipo_espaco_contratado, r.status
            FROM reserva r
            JOIN associacao a ON a.id=r.associacao_id
            JOIN usuario u ON u.id=@usuario
            WHERE r.id=@reserva AND (
                (@titular=TRUE AND u.pessoa_id=a.titular_pessoa_id)
                OR (@titular=FALSE AND u.pessoa_id=r.responsavel_pessoa_id
                    AND EXISTS (SELECT 1 FROM dependente_associacao d
                        WHERE d.associacao_id=a.id AND d.dependente_pessoa_id=u.pessoa_id
                          AND d.ativo=TRUE)))
            """, conexao))
        {
            reserva.Parameters.AddWithValue("@reserva", reservaId);
            reserva.Parameters.AddWithValue("@usuario", usuarioId);
            reserva.Parameters.AddWithValue("@titular", titular);
            await using var reader = await reserva.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            inicioUtc = DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc);
            tipo = reader.GetString(1);
            status = reader.GetString(2);
        }

        if (tipo is not ("SalaoFestas" or "Churrasqueira")) return null;

        var lista = new List<ConvidadoReservaResposta>();
        using (var convidados = new MySqlCommand("""
            SELECT id, nome, identificacao_minima
            FROM convidado_reserva
            WHERE reserva_id=@reserva AND situacao='Autorizado'
            ORDER BY id
            """, conexao))
        {
            convidados.Parameters.AddWithValue("@reserva", reservaId);
            await using var reader = await convidados.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                lista.Add(new ConvidadoReservaResposta(reader.GetInt64(0),
                    reader.GetString(1), reader.GetString(2)));
        }

        var limite = inicioUtc.AddHours(-24);
        return new ConvidadosReservaResposta(lista, limite,
            status == "Confirmada" && DateTime.UtcNow < limite);
    }

    public async Task<ResultadoConvidados> AtualizarAsync(
        long reservaId, long usuarioId, bool titular,
        AtualizacaoConvidadosEntrada entrada,
        CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        DateTime inicioUtc;
        string tipo, status;
        int participantes;
        long unidadeId;

        using (var reserva = new MySqlCommand("""
            SELECT r.inicio_utc, r.tipo_espaco_contratado, r.status,
                   r.quantidade_participantes, e.unidade_id
            FROM reserva r
            JOIN associacao a ON a.id=r.associacao_id
            JOIN usuario u ON u.id=@usuario
            JOIN espaco e ON e.id=r.espaco_id
            WHERE r.id=@reserva AND (
                (@titular=TRUE AND u.pessoa_id=a.titular_pessoa_id)
                OR (@titular=FALSE AND u.pessoa_id=r.responsavel_pessoa_id
                    AND EXISTS (SELECT 1 FROM dependente_associacao d
                        WHERE d.associacao_id=a.id AND d.dependente_pessoa_id=u.pessoa_id
                          AND d.ativo=TRUE)))
            FOR UPDATE
            """, conexao, transacao))
        {
            reserva.Parameters.AddWithValue("@reserva", reservaId);
            reserva.Parameters.AddWithValue("@usuario", usuarioId);
            reserva.Parameters.AddWithValue("@titular", titular);
            await using var reader = await reserva.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return ResultadoConvidados.ReservaNaoEncontrada;

            inicioUtc = DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc);
            tipo = reader.GetString(1);
            status = reader.GetString(2);
            participantes = reader.GetInt32(3);
            unidadeId = reader.GetInt64(4);
        }

        if (tipo is not ("SalaoFestas" or "Churrasqueira"))
            return ResultadoConvidados.TipoNaoPermiteConvidados;
        if (status != "Confirmada" ||
            DateTime.UtcNow >= inicioUtc.AddHours(-24))
            return ResultadoConvidados.ForaDoPrazo;
        if (!PersistenciaConvidados.ListaValida(
                entrada.Convidados, participantes))
            return ResultadoConvidados.ListaInvalida;

        await PersistenciaConvidados.SincronizarAsync(conexao, transacao,
            reservaId, entrada.Convidados, ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            unidadeId, "Atualizar", "ConvidadoReserva", reservaId,
            "Lista de convidados atualizada dentro do prazo de 24 horas.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoConvidados.Concluido;
    }
}
