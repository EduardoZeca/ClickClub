using System.Data;
using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class PortariaEventoRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<ValidacaoVisitanteEventoResposta?> ValidarAsync(
        ValidacaoVisitanteEventoEntrada entrada, long operadorId,
        bool administrador, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        string? unidadeStatus;
        using (var unidade = new MySqlCommand("""
            SELECT u.status FROM unidade u
            WHERE u.id=@unidade AND (@admin=TRUE OR EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@operador AND pu.unidade_id=u.id
                  AND pu.papel='Porteiro'))
            """, conexao, transacao))
        {
            unidade.Parameters.AddWithValue("@unidade", entrada.UnidadeId);
            unidade.Parameters.AddWithValue("@operador", operadorId);
            unidade.Parameters.AddWithValue("@admin", administrador);
            unidadeStatus = await unidade.ExecuteScalarAsync(ct) as string;
        }
        if (unidadeStatus is null) return null;

        var agora = DateTime.UtcNow;
        long? convidadoId = null;
        long? eventoId = null;
        string? nome = null;
        string motivo;

        if (unidadeStatus != "Ativa")
            motivo = "UnidadeInativa";
        else if (!entrada.IdentidadeConferida)
            motivo = "IdentidadeNaoConferida";
        else
        {
            using var cmd = new MySqlCommand("""
                SELECT e.id, e.status, e.publico_alvo,
                       e.inicio_utc, e.fim_utc,
                       v.id, v.nome, v.situacao
                FROM evento_social e
                LEFT JOIN convidado_evento v
                  ON v.evento_id=e.id AND v.identificacao_minima=@identificacao
                WHERE e.id=@evento AND e.unidade_id=@unidade
                FOR SHARE
                """, conexao, transacao);
            cmd.Parameters.AddWithValue("@evento", entrada.EventoId);
            cmd.Parameters.AddWithValue("@unidade", entrada.UnidadeId);
            cmd.Parameters.AddWithValue("@identificacao",
                entrada.IdentificacaoMinima!.Trim());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                motivo = "EventoNaoEncontrado";
            else
            {
                eventoId = reader.GetInt64(0);
                var statusEvento = reader.GetString(1);
                var publico = reader.GetString(2);
                var inicio = DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc);
                var fim = DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc);
                if (!reader.IsDBNull(5))
                {
                    convidadoId = reader.GetInt64(5);
                    nome = reader.GetString(6);
                }
                if (publico != "PublicoGeral") motivo = "EventoNaoPublico";
                else if (statusEvento != "Publicado") motivo = "EventoIndisponivel";
                else if (agora < inicio || agora >= fim) motivo = "ForaDoPeriodo";
                else if (!convidadoId.HasValue) motivo = "VisitanteNaoCadastrado";
                else if (reader.GetString(7) != "Autorizado") motivo = "VisitanteRevogado";
                else motivo = "Permitido";
            }
        }

        var permitido = motivo == "Permitido";
        using var registrar = new MySqlCommand("""
            INSERT INTO registro_acesso
                (unidade_id, evento_id, convidado_evento_id,
                 porteiro_usuario_id, tipo, resultado,
                 motivo_codigo, data_hora_utc)
            VALUES (@unidade, @evento, @visitante, @operador,
                    'EventoPublico', @resultado, @motivo, @agora)
            """, conexao, transacao);
        registrar.Parameters.AddWithValue("@unidade", entrada.UnidadeId);
        registrar.Parameters.AddWithValue("@evento",
            eventoId.HasValue ? eventoId.Value : DBNull.Value);
        registrar.Parameters.AddWithValue("@visitante",
            convidadoId.HasValue ? convidadoId.Value : DBNull.Value);
        registrar.Parameters.AddWithValue("@operador", operadorId);
        registrar.Parameters.AddWithValue("@resultado", permitido ? "Permitido" : "Negado");
        registrar.Parameters.AddWithValue("@motivo", motivo);
        registrar.Parameters.AddWithValue("@agora", agora);
        await registrar.ExecuteNonQueryAsync(ct);
        var registroId = registrar.LastInsertedId;
        await transacao.CommitAsync(ct);

        var mensagem = motivo switch
        {
            "Permitido" => "Visitante cadastrado e identificado. Entrada autorizada para este evento.",
            "IdentidadeNaoConferida" => "Conferência presencial de documento obrigatória.",
            "EventoNaoEncontrado" => "Evento não encontrado neste clube.",
            "EventoNaoPublico" => "O evento é exclusivo para associados.",
            "EventoIndisponivel" => "Evento cancelado ou encerrado.",
            "ForaDoPeriodo" => "Fora do período de realização do evento.",
            "VisitanteNaoCadastrado" => "Visitante não cadastrado para este evento.",
            "VisitanteRevogado" => "Autorização do visitante revogada.",
            _ => "Clube indisponível para entrada."
        };
        return new ValidacaoVisitanteEventoResposta(registroId,
            permitido, permitido ? "Permitido" : "Negado", motivo,
            mensagem, nome, agora);
    }
}
