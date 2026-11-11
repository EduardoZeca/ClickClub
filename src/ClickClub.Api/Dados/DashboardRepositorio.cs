using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

// Relatorio somente leitura. Escopos financeiro e operacional são avaliados
// por unidade no banco, inclusive quando o usuário acumula papéis distintos.
public sealed class DashboardRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<(bool ForaDoEscopo, DashboardResposta Dados)> ConsultarAsync(
        long usuarioId, bool admin, long? unidadeId,
        DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        var inicioUtc = inicio.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var fimExclusivoUtc = fim.AddDays(1)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);

        // Uma unidade só aparece quando o usuário tem atribuição vigente nela.
        // A coluna financeiro indica acesso à cobrança de sua unidade de origem.
        using var clubes = new MySqlCommand("""
            SELECT un.id, un.nome,
                (@admin=TRUE OR EXISTS (
                    SELECT 1 FROM papel_usuario pu
                    WHERE pu.usuario_id=@usuario AND pu.unidade_id=un.id
                      AND pu.papel='Tesoureiro')) AS pode_financeiro
            FROM unidade un
            WHERE (@unidade IS NULL OR un.id=@unidade)
              AND (@admin=TRUE OR EXISTS (
                  SELECT 1 FROM papel_usuario pu
                  WHERE pu.usuario_id=@usuario AND pu.unidade_id=un.id
                    AND pu.papel IN ('PresidenteClube','FuncionarioUnidade','Tesoureiro')))
            ORDER BY un.nome, un.id
            """, conexao);
        clubes.Parameters.AddWithValue("@admin", admin);
        clubes.Parameters.AddWithValue("@usuario", usuarioId);
        clubes.Parameters.AddWithValue("@unidade", unidadeId.HasValue
            ? unidadeId.Value : DBNull.Value);

        var escopos = new List<(long Id, string Nome, bool Financeiro)>();
        await using (var reader = await clubes.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                escopos.Add((reader.GetInt64(0), reader.GetString(1),
                    reader.GetBoolean(2)));
        }

        if (unidadeId.HasValue && escopos.Count == 0)
            return (true, new DashboardResposta(inicio, fim, []));

        var unidades = new List<DashboardUnidadeResumo>();
        foreach (var escopo in escopos)
        {
            // Intervalos de reserva/turma/evento consideram qualquer sobreposição
            // com [inicio 00:00 UTC, dia seguinte ao fim 00:00 UTC).
            using var operacao = new MySqlCommand("""
                SELECT
                    (SELECT COUNT(*) FROM espaco e
                     WHERE e.unidade_id=@unidade AND e.ativo=TRUE),
                    (SELECT COUNT(DISTINCT r.espaco_id)
                     FROM reserva r JOIN espaco e ON e.id=r.espaco_id
                     WHERE e.unidade_id=@unidade AND e.ativo=TRUE
                       AND r.status='Confirmada'
                       AND r.inicio_utc<@fim AND r.fim_utc>@inicio),
                    (SELECT COUNT(*) FROM reserva r
                     JOIN espaco e ON e.id=r.espaco_id
                     WHERE e.unidade_id=@unidade AND r.status='Confirmada'
                       AND r.inicio_utc<@fim AND r.fim_utc>@inicio),
                    (SELECT COUNT(*) FROM inscricao_turma i
                     JOIN turma t ON t.id=i.turma_id
                     WHERE t.unidade_id=@unidade AND t.status<>'Cancelada'
                       AND i.status='Confirmada'
                       AND t.inicio_utc<@fim AND t.fim_utc>@inicio),
                    (SELECT COUNT(*) FROM inscricao_turma i
                     JOIN turma t ON t.id=i.turma_id
                     WHERE t.unidade_id=@unidade AND t.status<>'Cancelada'
                       AND i.status='EmEspera'
                       AND t.inicio_utc<@fim AND t.fim_utc>@inicio),
                    (SELECT COUNT(*) FROM inscricao_evento i
                     JOIN evento_social ev ON ev.id=i.evento_id
                     WHERE ev.unidade_id=@unidade AND ev.status<>'Cancelado'
                       AND i.status='Confirmada'
                       AND ev.inicio_utc<@fim AND ev.fim_utc>@inicio)
                """, conexao);
            operacao.Parameters.AddWithValue("@unidade", escopo.Id);
            operacao.Parameters.AddWithValue("@inicio", inicioUtc);
            operacao.Parameters.AddWithValue("@fim", fimExclusivoUtc);

            long espacos, ocupados, reservas, turmas, espera, eventos;
            await using (var reader = await operacao.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Resumo operacional não retornado.");
                espacos = reader.GetInt64(0);
                ocupados = reader.GetInt64(1);
                reservas = reader.GetInt64(2);
                turmas = reader.GetInt64(3);
                espera = reader.GetInt64(4);
                eventos = reader.GetInt64(5);
            }

            DashboardFinanceiroResumo? financeiro = null;
            if (escopo.Financeiro)
            {
                // Vencida = vencimento anterior à data UTC de hoje, mas não
                // implica bloqueio associativo (que usa tolerância de 10 dias).
                // Competência determina o período; valores são apenas simulados.
                using var faturas = new MySqlCommand("""
                    SELECT
                        COUNT(CASE WHEN status='QuitadaSimulada' THEN 1 END),
                        COALESCE(SUM(CASE WHEN status='QuitadaSimulada'
                            THEN valor_total ELSE 0 END), 0),
                        COUNT(CASE WHEN status='EmAberto'
                            AND data_vencimento>=UTC_DATE() THEN 1 END),
                        COALESCE(SUM(CASE WHEN status='EmAberto'
                            AND data_vencimento>=UTC_DATE()
                            THEN valor_total ELSE 0 END), 0),
                        COUNT(CASE WHEN status='EmAberto'
                            AND data_vencimento<UTC_DATE() THEN 1 END),
                        COALESCE(SUM(CASE WHEN status='EmAberto'
                            AND data_vencimento<UTC_DATE()
                            THEN valor_total ELSE 0 END), 0),
                        COUNT(CASE WHEN status='EmPreparacao' THEN 1 END)
                    FROM fatura
                    WHERE unidade_origem_id=@unidade
                      AND competencia>=@inicio AND competencia<@fim
                      AND (@admin=TRUE OR EXISTS (
                          SELECT 1 FROM papel_usuario pu
                          WHERE pu.usuario_id=@usuario AND pu.unidade_id=@unidade
                            AND pu.papel='Tesoureiro'))
                    """, conexao);
                faturas.Parameters.AddWithValue("@unidade", escopo.Id);
                faturas.Parameters.AddWithValue("@inicio", inicioUtc);
                faturas.Parameters.AddWithValue("@fim", fimExclusivoUtc);
                faturas.Parameters.AddWithValue("@admin", admin);
                faturas.Parameters.AddWithValue("@usuario", usuarioId);
                await using var reader = await faturas.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Resumo financeiro não retornado.");
                financeiro = new DashboardFinanceiroResumo(
                    reader.GetInt64(0), reader.GetDecimal(1),
                    reader.GetInt64(2), reader.GetDecimal(3),
                    reader.GetInt64(4), reader.GetDecimal(5),
                    reader.GetInt64(6));
            }

            unidades.Add(new DashboardUnidadeResumo(escopo.Id, escopo.Nome,
                espacos, ocupados, reservas, turmas, espera, eventos, financeiro));
        }

        return (false, new DashboardResposta(inicio, fim, unidades));
    }
}
