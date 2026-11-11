using System.Data;
using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class InscricaoTurmaRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    private sealed record TurmaAtual(long UnidadeId, int Capacidade,
        string Status, DateTime InicioUtc, DateTime FimUtc,
        string StatusUnidade, bool EscopoPermitido);

    public async Task<(ResultadoInscricaoTurma Resultado,
        InscricaoTurmaCriada? Inscricao)> InscreverMinhaAsync(
        long turmaId, long usuarioId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var pessoaId = await PessoaDoUsuarioAsync(conexao, usuarioId, ct);
        if (!pessoaId.HasValue)
            return (ResultadoInscricaoTurma.SemAssociacaoAtiva, null);

        // Sempre travar a pessoa antes da turma. Assim duas inscrições
        // concorrentes do mesmo associado não contornam RN12 em clubes distintos.
        await using var transacao = await conexao.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);
        await TravarPessoaAsync(conexao, transacao, pessoaId.Value, ct);
        var elegibilidade = await ElegibilidadeAsync(
            conexao, transacao, pessoaId.Value, ct);
        if (elegibilidade != ResultadoInscricaoTurma.Concluida)
            return (elegibilidade, null);

        var turma = await TurmaAtualAsync(conexao, transacao, turmaId,
            usuarioId, true, ct);
        if (turma is null)
            return (ResultadoInscricaoTurma.TurmaNaoEncontrada, null);
        if (!PodeInscrever(turma))
            return (ResultadoInscricaoTurma.TurmaIndisponivel, null);

        using (var duplicada = new MySqlCommand("""
            SELECT id FROM inscricao_turma
            WHERE turma_id=@turma AND pessoa_id=@pessoa
              AND status IN ('Confirmada','EmEspera') LIMIT 1
            """, conexao, transacao))
        {
            duplicada.Parameters.AddWithValue("@turma", turmaId);
            duplicada.Parameters.AddWithValue("@pessoa", pessoaId.Value);
            if (await duplicada.ExecuteScalarAsync(ct) is not null)
                return (ResultadoInscricaoTurma.InscricaoDuplicada, null);
        }

        if (await ExisteConflitoAsync(conexao, transacao, pessoaId.Value,
                0, turma.InicioUtc, turma.FimUtc, ct))
            return (ResultadoInscricaoTurma.ConflitoHorario, null);

        var ocupadas = await ContarAsync(conexao, transacao, turmaId,
            "Confirmada", ct);
        var emEspera = await ContarAsync(conexao, transacao, turmaId,
            "EmEspera", ct);
        // Mesmo com vaga, novas inscrições não ultrapassam quem já está
        // esperando: o funcionário decide manualmente a promoção.
        var status = ocupadas < turma.Capacidade && emEspera == 0
            ? "Confirmada" : "EmEspera";

        using var inserir = new MySqlCommand("""
            INSERT INTO inscricao_turma
                (turma_id, pessoa_id, status, inscrita_em_utc)
            VALUES (@turma, @pessoa, @status, UTC_TIMESTAMP(6))
            """, conexao, transacao);
        inserir.Parameters.AddWithValue("@turma", turmaId);
        inserir.Parameters.AddWithValue("@pessoa", pessoaId.Value);
        inserir.Parameters.AddWithValue("@status", status);
        await inserir.ExecuteNonQueryAsync(ct);
        var id = inserir.LastInsertedId;
        var posicao = status == "EmEspera"
            ? await ContarAsync(conexao, transacao, turmaId, "EmEspera", ct)
            : (int?)null;

        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            turma.UnidadeId, "Inscrever", "InscricaoTurma", id,
            "Inscrição criada com situação " + status + ".", ct);
        await transacao.CommitAsync(ct);
        return (ResultadoInscricaoTurma.Concluida,
            new InscricaoTurmaCriada(id, turmaId, status, posicao));
    }

    public async Task<ResultadoInscricaoTurma> CancelarMinhaAsync(
        long turmaId, long usuarioId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var pessoaId = await PessoaDoUsuarioAsync(conexao, usuarioId, ct);
        if (!pessoaId.HasValue)
            return ResultadoInscricaoTurma.InscricaoNaoEncontrada;
        await using var transacao = await conexao.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);
        await TravarPessoaAsync(conexao, transacao, pessoaId.Value, ct);
        var turma = await TurmaAtualAsync(conexao, transacao, turmaId,
            usuarioId, true, ct);
        if (turma is null) return ResultadoInscricaoTurma.TurmaNaoEncontrada;

        long inscricaoId;
        string status;
        using (var consultar = new MySqlCommand("""
            SELECT id, status FROM inscricao_turma
            WHERE turma_id=@turma AND pessoa_id=@pessoa
            ORDER BY (status<>'Cancelada') DESC, id DESC LIMIT 1 FOR UPDATE
            """, conexao, transacao))
        {
            consultar.Parameters.AddWithValue("@turma", turmaId);
            consultar.Parameters.AddWithValue("@pessoa", pessoaId.Value);
            await using var reader = await consultar.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return ResultadoInscricaoTurma.InscricaoNaoEncontrada;
            inscricaoId = reader.GetInt64(0);
            status = reader.GetString(1);
        }
        if (status == "Cancelada")
            return ResultadoInscricaoTurma.InscricaoJaCancelada;

        using var cmd = new MySqlCommand("""
            UPDATE inscricao_turma
            SET status='Cancelada', cancelada_em_utc=UTC_TIMESTAMP(6)
            WHERE id=@id
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@id", inscricaoId);
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            turma.UnidadeId, "Cancelar", "InscricaoTurma", inscricaoId,
            "Inscrição cancelada pelo próprio associado.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoInscricaoTurma.Concluida;
    }

    public async Task<List<MinhaInscricaoTurmaItem>> MinhasAsync(
        long usuarioId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT i.id, i.turma_id, a.nome, u.nome, u.fuso_horario,
                   t.inicio_utc, t.fim_utc, i.status,
                   CASE WHEN i.status='EmEspera' THEN (
                       SELECT COUNT(*) FROM inscricao_turma fila
                       WHERE fila.turma_id=i.turma_id AND fila.status='EmEspera'
                         AND (fila.inscrita_em_utc<i.inscrita_em_utc
                           OR (fila.inscrita_em_utc=i.inscrita_em_utc
                               AND fila.id<=i.id))) ELSE NULL END AS posicao
            FROM inscricao_turma i
            JOIN turma t ON t.id=i.turma_id
            JOIN atividade a ON a.id=t.atividade_id
            JOIN unidade u ON u.id=t.unidade_id
            JOIN usuario us ON us.pessoa_id=i.pessoa_id
            WHERE us.id=@usuario
            ORDER BY i.inscrita_em_utc DESC, i.id DESC
            """, conexao);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        var itens = new List<MinhaInscricaoTurmaItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var fuso = reader.GetString(4);
            itens.Add(new(reader.GetInt64(0), reader.GetInt64(1),
                reader.GetString(2), reader.GetString(3),
                ParaLocal(reader.GetDateTime(5), fuso),
                ParaLocal(reader.GetDateTime(6), fuso), reader.GetString(7),
                reader.IsDBNull(8) ? null : Convert.ToInt32(reader.GetValue(8))));
        }
        return itens;
    }

    public async Task<VagasTurmaResposta?> VagasAsync(
        long turmaId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT t.id, t.capacidade,
                   (SELECT COUNT(*) FROM inscricao_turma i
                    WHERE i.turma_id=t.id AND i.status='Confirmada'),
                   (SELECT COUNT(*) FROM inscricao_turma i
                    WHERE i.turma_id=t.id AND i.status='EmEspera')
            FROM turma t WHERE t.id=@id
            """, conexao);
        cmd.Parameters.AddWithValue("@id", turmaId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var capacidade = reader.GetInt32(1);
        var confirmadas = Convert.ToInt32(reader.GetValue(2));
        var esperando = Convert.ToInt32(reader.GetValue(3));
        return new(reader.GetInt64(0), capacidade, confirmadas, esperando,
            Math.Max(0, capacidade - confirmadas));
    }

    public async Task<(ResultadoInscricaoTurma Resultado,
        List<InscricaoTurmaDetalhe> Itens)> ListarGestaoAsync(
        long turmaId, long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var escopo = await ConferirEscopoAsync(conexao, turmaId,
            usuarioId, admin, ct);
        if (escopo != ResultadoInscricaoTurma.Concluida)
            return (escopo, []);

        using var cmd = new MySqlCommand("""
            SELECT i.id, i.turma_id, i.pessoa_id, p.nome,
                   i.status, i.inscrita_em_utc,
                   CASE WHEN i.status='EmEspera' THEN (
                       SELECT COUNT(*) FROM inscricao_turma fila
                       WHERE fila.turma_id=i.turma_id AND fila.status='EmEspera'
                         AND (fila.inscrita_em_utc<i.inscrita_em_utc
                           OR (fila.inscrita_em_utc=i.inscrita_em_utc
                               AND fila.id<=i.id))) ELSE NULL END
            FROM inscricao_turma i
            JOIN pessoa p ON p.id=i.pessoa_id
            WHERE i.turma_id=@turma
            ORDER BY FIELD(i.status,'Confirmada','EmEspera','Cancelada'),
                     i.inscrita_em_utc, i.id
            """, conexao);
        cmd.Parameters.AddWithValue("@turma", turmaId);
        var itens = new List<InscricaoTurmaDetalhe>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            itens.Add(new(reader.GetInt64(0), reader.GetInt64(1),
                reader.GetInt64(2), reader.GetString(3), reader.GetString(4),
                DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc),
                reader.IsDBNull(6) ? null : Convert.ToInt32(reader.GetValue(6))));
        return (ResultadoInscricaoTurma.Concluida, itens);
    }

    // A ação é sempre disparada pelo funcionário; a API escolhe o primeiro
    // elegível pela ordem original da fila, sem aceitar ID arbitrário.
    public async Task<(ResultadoInscricaoTurma Resultado,
        PromocaoTurmaResposta? Promovida)> PromoverProximoAsync(
        long turmaId, long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var escopo = await ConferirEscopoAsync(conexao, turmaId,
            usuarioId, admin, ct);
        if (escopo != ResultadoInscricaoTurma.Concluida)
            return (escopo, null);

        // Responder corretamente mesmo quando não há ninguém na espera.
        using (var situacao = new MySqlCommand("""
            SELECT t.capacidade, t.status, t.inicio_utc, u.status,
                (SELECT COUNT(*) FROM inscricao_turma i
                 WHERE i.turma_id=t.id AND i.status='Confirmada')
            FROM turma t JOIN unidade u ON u.id=t.unidade_id
            WHERE t.id=@turma
            """, conexao))
        {
            situacao.Parameters.AddWithValue("@turma", turmaId);
            await using var reader = await situacao.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return (ResultadoInscricaoTurma.TurmaNaoEncontrada, null);
            if (reader.GetString(1) != "Aberta" ||
                reader.GetString(3) != "Ativa" ||
                reader.GetDateTime(2) <= DateTime.UtcNow)
                return (ResultadoInscricaoTurma.TurmaIndisponivel, null);
            if (Convert.ToInt32(reader.GetValue(4)) >= reader.GetInt32(0))
                return (ResultadoInscricaoTurma.SemVagas, null);
        }

        var candidatos = new List<(long Id, long PessoaId, string Nome)>();
        using (var buscar = new MySqlCommand("""
            SELECT i.id, i.pessoa_id, p.nome
            FROM inscricao_turma i JOIN pessoa p ON p.id=i.pessoa_id
            WHERE i.turma_id=@turma AND i.status='EmEspera'
            ORDER BY i.inscrita_em_utc, i.id
            """, conexao))
        {
            buscar.Parameters.AddWithValue("@turma", turmaId);
            await using var reader = await buscar.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                candidatos.Add((reader.GetInt64(0), reader.GetInt64(1),
                    reader.GetString(2)));
        }

        // Pessoa -> turma: mesma ordem de travas usada na inscrição e no cancelamento.
        foreach (var candidato in candidatos)
        {
            await using var transacao = await conexao.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);
            await TravarPessoaAsync(conexao, transacao, candidato.PessoaId, ct);
            var turma = await TurmaAtualAsync(conexao, transacao,
                turmaId, usuarioId, admin, ct);
            if (turma is null)
                return (ResultadoInscricaoTurma.TurmaNaoEncontrada, null);
            if (!turma.EscopoPermitido)
                return (ResultadoInscricaoTurma.ForaDoEscopo, null);
            if (!PodeInscrever(turma))
                return (ResultadoInscricaoTurma.TurmaIndisponivel, null);
            if (await ContarAsync(conexao, transacao, turmaId,
                    "Confirmada", ct) >= turma.Capacidade)
                return (ResultadoInscricaoTurma.SemVagas, null);

            using (var conferir = new MySqlCommand("""
                SELECT status FROM inscricao_turma
                WHERE id=@id AND turma_id=@turma AND pessoa_id=@pessoa
                FOR UPDATE
                """, conexao, transacao))
            {
                conferir.Parameters.AddWithValue("@id", candidato.Id);
                conferir.Parameters.AddWithValue("@turma", turmaId);
                conferir.Parameters.AddWithValue("@pessoa", candidato.PessoaId);
                if (await conferir.ExecuteScalarAsync(ct) is not string status ||
                    status != "EmEspera")
                    continue; // Outra solicitação já promoveu/cancelou este candidato.
            }

            if (await ElegibilidadeAsync(conexao, transacao,
                    candidato.PessoaId, ct) != ResultadoInscricaoTurma.Concluida)
                continue;
            if (await ExisteConflitoAsync(conexao, transacao,
                    candidato.PessoaId, candidato.Id,
                    turma.InicioUtc, turma.FimUtc, ct))
                continue;

            using (var promover = new MySqlCommand("""
                UPDATE inscricao_turma SET status='Confirmada'
                WHERE id=@id AND status='EmEspera'
                """, conexao, transacao))
            {
                promover.Parameters.AddWithValue("@id", candidato.Id);
                if (await promover.ExecuteNonQueryAsync(ct) != 1) continue;
            }
            await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
                turma.UnidadeId, "Promover", "InscricaoTurma", candidato.Id,
                "Primeiro candidato elegível promovido manualmente.", ct);
            await transacao.CommitAsync(ct);
            return (ResultadoInscricaoTurma.Concluida,
                new PromocaoTurmaResposta(candidato.Id, turmaId,
                    candidato.PessoaId, candidato.Nome));
        }
        return (ResultadoInscricaoTurma.SemCandidatoElegivel, null);
    }

    private static async Task<long?> PessoaDoUsuarioAsync(
        MySqlConnection conexao, long usuarioId, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT pessoa_id FROM usuario WHERE id=@usuario AND ativo=TRUE
            """, conexao);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        var id = await cmd.ExecuteScalarAsync(ct);
        return id is null ? null : Convert.ToInt64(id);
    }

    private static async Task TravarPessoaAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long pessoaId, CancellationToken ct)
    {
        using var cmd = new MySqlCommand(
            "SELECT id FROM pessoa WHERE id=@id FOR UPDATE", conexao, transacao);
        cmd.Parameters.AddWithValue("@id", pessoaId);
        await cmd.ExecuteScalarAsync(ct);
    }

    private static async Task<ResultadoInscricaoTurma> ElegibilidadeAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long pessoaId, CancellationToken ct)
    {
        long associacaoId;
        string fuso;
        using (var cmd = new MySqlCommand("""
            SELECT a.id, origem.fuso_horario
            FROM associacao a
            JOIN unidade origem ON origem.id=a.unidade_origem_id
            JOIN pessoa p ON p.id=@pessoa AND p.status='Ativa'
            JOIN usuario us ON us.pessoa_id=p.id AND us.ativo=TRUE
            WHERE a.status='Ativa'
              AND (a.titular_pessoa_id=p.id OR EXISTS (
                  SELECT 1 FROM dependente_associacao d
                  WHERE d.associacao_id=a.id
                    AND d.dependente_pessoa_id=p.id AND d.ativo=TRUE))
              AND EXISTS (SELECT 1 FROM papel_usuario pu
                  WHERE pu.usuario_id=us.id
                    AND pu.papel IN ('Titular','Dependente'))
            ORDER BY a.id LIMIT 1
            """, conexao, transacao))
        {
            cmd.Parameters.AddWithValue("@pessoa", pessoaId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return ResultadoInscricaoTurma.SemAssociacaoAtiva;
            associacaoId = reader.GetInt64(0);
            fuso = reader.GetString(1);
        }

        DateTime hojeOrigem;
        try { hojeOrigem = RegraInadimplencia.DataLocal(DateTime.UtcNow, fuso); }
        catch (InvalidOperationException)
        { return ResultadoInscricaoTurma.FusoInvalido; }

        using var atraso = new MySqlCommand("""
            SELECT EXISTS(SELECT 1 FROM fatura f
                WHERE f.associacao_id=@associacao
                  AND f.tipo='Mensalidade' AND f.status='EmAberto'
                  AND f.valor_total>0 AND f.data_vencimento<@limite)
            """, conexao, transacao);
        atraso.Parameters.AddWithValue("@associacao", associacaoId);
        atraso.Parameters.AddWithValue("@limite",
            hojeOrigem.AddDays(-RegraInadimplencia.DiasTolerancia));
        return Convert.ToInt32(await atraso.ExecuteScalarAsync(ct)) == 1
            ? ResultadoInscricaoTurma.MensalidadeInadimplente
            : ResultadoInscricaoTurma.Concluida;
    }

    private static async Task<TurmaAtual?> TurmaAtualAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long turmaId, long usuarioId, bool admin, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT t.unidade_id, t.capacidade, t.status,
                   t.inicio_utc, t.fim_utc, u.status,
                   (@admin=TRUE OR EXISTS (
                       SELECT 1 FROM papel_usuario pu
                       WHERE pu.usuario_id=@usuario AND pu.unidade_id=t.unidade_id
                         AND pu.papel='FuncionarioUnidade')) AS permitido
            FROM turma t JOIN unidade u ON u.id=t.unidade_id
            WHERE t.id=@turma FOR UPDATE
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@turma", turmaId);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@admin", admin);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new TurmaAtual(reader.GetInt64(0), reader.GetInt32(1),
                reader.GetString(2), reader.GetDateTime(3), reader.GetDateTime(4),
                reader.GetString(5), reader.GetBoolean(6))
            : null;
    }

    private static bool PodeInscrever(TurmaAtual turma) =>
        turma.Status == "Aberta" && turma.StatusUnidade == "Ativa" &&
        turma.InicioUtc > DateTime.UtcNow;

    private static async Task<ResultadoInscricaoTurma> ConferirEscopoAsync(
        MySqlConnection conexao, long turmaId, long usuarioId,
        bool admin, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT (@admin=TRUE OR EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.unidade_id=t.unidade_id
                  AND pu.papel='FuncionarioUnidade'))
            FROM turma t WHERE t.id=@turma
            """, conexao);
        cmd.Parameters.AddWithValue("@turma", turmaId);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@admin", admin);
        var escopo = await cmd.ExecuteScalarAsync(ct);
        if (escopo is null) return ResultadoInscricaoTurma.TurmaNaoEncontrada;
        return Convert.ToBoolean(escopo)
            ? ResultadoInscricaoTurma.Concluida : ResultadoInscricaoTurma.ForaDoEscopo;
    }

    private static async Task<int> ContarAsync(MySqlConnection conexao,
        MySqlTransaction transacao, long turmaId, string status,
        CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT COUNT(*) FROM inscricao_turma
            WHERE turma_id=@turma AND status=@status
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@turma", turmaId);
        cmd.Parameters.AddWithValue("@status", status);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task<bool> ExisteConflitoAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long pessoaId, long ignorarInscricaoId, DateTime inicioUtc,
        DateTime fimUtc, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT EXISTS(SELECT 1 FROM inscricao_turma i
                JOIN turma t ON t.id=i.turma_id
                WHERE i.pessoa_id=@pessoa AND i.id<>@ignorar
                  AND i.status IN ('Confirmada','EmEspera')
                  AND t.status<>'Cancelada'
                  AND t.inicio_utc<@fim AND t.fim_utc>@inicio)
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@pessoa", pessoaId);
        cmd.Parameters.AddWithValue("@ignorar", ignorarInscricaoId);
        cmd.Parameters.AddWithValue("@inicio", inicioUtc);
        cmd.Parameters.AddWithValue("@fim", fimUtc);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
    }

    private static DateTime ParaLocal(DateTime instanteUtc, string fuso)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out var zona))
            throw new InvalidOperationException("Fuso horário de clube inválido.");
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(instanteUtc, DateTimeKind.Utc), zona),
            DateTimeKind.Unspecified);
    }
}
