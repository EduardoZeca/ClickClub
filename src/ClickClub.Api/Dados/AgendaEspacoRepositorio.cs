using System.Globalization;
using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class AgendaEspacoRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<(ResultadoAgenda Resultado, List<HorarioEspacoItem> Itens)>
        ListarHorariosAsync(long espacoId, long usuarioId, bool admin,
            CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var (resultado, _) = await VerificarEspacoAsync(
            conexao, null, espacoId, usuarioId, admin, false, ct);
        if (resultado != ResultadoAgenda.Concluido) return (resultado, []);

        using var cmd = new MySqlCommand("""
            SELECT id, espaco_id, dia_semana, inicio, fim, fim_dia_seguinte, ativo
            FROM horario_espaco WHERE espaco_id=@espaco
            ORDER BY dia_semana, inicio, id
            """, conexao);
        cmd.Parameters.AddWithValue("@espaco", espacoId);
        var itens = new List<HorarioEspacoItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            itens.Add(LerHorario(reader));
        return (ResultadoAgenda.Concluido, itens);
    }

    public async Task<ResultadoAgenda> CriarHorarioAsync(
        long espacoId, HorarioEspacoEntrada entrada, long usuarioId,
        bool admin, CancellationToken ct)
    {
        if (!ValidarHorario(entrada, out var inicio, out var fim))
            return ResultadoAgenda.PeriodoInvalido;

        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, _) = await VerificarEspacoAsync(
            conexao, transacao, espacoId, usuarioId, admin, true, ct);
        if (resultado != ResultadoAgenda.Concluido) return resultado;

        if (await ExisteConflitoSemanalAsync(conexao, transacao, espacoId,
                0, entrada.DiaSemana, inicio, fim, entrada.FimDiaSeguinte, ct))
            return ResultadoAgenda.ConflitoHorario;

        using var cmd = new MySqlCommand("""
            INSERT INTO horario_espaco
                (espaco_id, dia_semana, inicio, fim, fim_dia_seguinte, ativo)
            VALUES (@espaco, @dia, @inicio, @fim, @seguinte, TRUE)
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@espaco", espacoId);
        cmd.Parameters.AddWithValue("@dia", entrada.DiaSemana);
        cmd.Parameters.AddWithValue("@inicio", inicio);
        cmd.Parameters.AddWithValue("@fim", fim);
        cmd.Parameters.AddWithValue("@seguinte", entrada.FimDiaSeguinte);
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            await UnidadeDoEspacoAsync(conexao, transacao, espacoId, ct),
            "CriarHorario", "HorarioEspaco", cmd.LastInsertedId,
            "Horário semanal cadastrado.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoAgenda.Concluido;
    }

    public async Task<ResultadoAgenda> AlterarHorarioAsync(
        long espacoId, long horarioId, bool ativo, long usuarioId,
        bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, _) = await VerificarEspacoAsync(
            conexao, transacao, espacoId, usuarioId, admin, true, ct);
        if (resultado != ResultadoAgenda.Concluido) return resultado;

        int dia;
        TimeSpan inicio, fim;
        bool seguinte;
        using (var consultar = new MySqlCommand("""
            SELECT dia_semana, inicio, fim, fim_dia_seguinte
            FROM horario_espaco WHERE id=@id AND espaco_id=@espaco FOR UPDATE
            """, conexao, transacao))
        {
            consultar.Parameters.AddWithValue("@id", horarioId);
            consultar.Parameters.AddWithValue("@espaco", espacoId);
            await using var reader = await consultar.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return ResultadoAgenda.NaoEncontrado;
            dia = Convert.ToInt32(reader.GetValue(0));
            inicio = reader.GetTimeSpan(1);
            fim = reader.GetTimeSpan(2);
            seguinte = reader.GetBoolean(3);
        }

        if (ativo && await ExisteConflitoSemanalAsync(conexao, transacao,
                espacoId, horarioId, dia, inicio, fim, seguinte, ct))
            return ResultadoAgenda.ConflitoHorario;

        using var cmd = new MySqlCommand("""
            UPDATE horario_espaco SET ativo=@ativo WHERE id=@id AND espaco_id=@espaco
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@ativo", ativo);
        cmd.Parameters.AddWithValue("@id", horarioId);
        cmd.Parameters.AddWithValue("@espaco", espacoId);
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            await UnidadeDoEspacoAsync(conexao, transacao, espacoId, ct),
            ativo ? "ReativarHorario" : "DesativarHorario",
            "HorarioEspaco", horarioId, "Situação do horário alterada.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoAgenda.Concluido;
    }

    public async Task<(ResultadoAgenda Resultado, List<ManutencaoEspacoItem> Itens)>
        ListarManutencoesAsync(long espacoId, long usuarioId, bool admin,
            CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var (resultado, fuso) = await VerificarEspacoAsync(
            conexao, null, espacoId, usuarioId, admin, false, ct);
        if (resultado != ResultadoAgenda.Concluido) return (resultado, []);
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso!, out var zona))
            return (ResultadoAgenda.FusoInvalido, []);

        using var cmd = new MySqlCommand("""
            SELECT id, espaco_id, inicio_utc, fim_utc, motivo, ativo
            FROM indisponibilidade_espaco WHERE espaco_id=@espaco
            ORDER BY inicio_utc DESC, id DESC
            """, conexao);
        cmd.Parameters.AddWithValue("@espaco", espacoId);
        var lista = new List<ManutencaoEspacoItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(new ManutencaoEspacoItem(reader.GetInt64(0),
                reader.GetInt64(1), ParaLocal(reader.GetDateTime(2), zona),
                ParaLocal(reader.GetDateTime(3), zona),
                reader.GetString(4), reader.GetBoolean(5)));
        return (ResultadoAgenda.Concluido, lista);
    }

    public async Task<(ResultadoAgenda Resultado, List<OcupacaoEspacoItem> Itens)>
        ListarOcupacoesAsync(long espacoId, int ano, int mes,
            long usuarioId, bool admin, CancellationToken ct)
    {
        if (ano is < 2000 or > 2100 || mes is < 1 or > 12)
            return (ResultadoAgenda.PeriodoInvalido, []);

        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var (resultado, fuso) = await VerificarEspacoAsync(
            conexao, null, espacoId, usuarioId, admin, false, ct);
        if (resultado != ResultadoAgenda.Concluido) return (resultado, []);
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso!, out var zona))
            return (ResultadoAgenda.FusoInvalido, []);

        var primeiroDia = new DateTime(ano, mes, 1);
        var inicioCalendario = primeiroDia.AddDays(
            -(((int)primeiroDia.DayOfWeek + 6) % 7));
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);
        var fimCalendario = ultimoDia.AddDays(
            7 - (((int)ultimoDia.DayOfWeek + 6) % 7));
        DateTime inicioUtc, fimUtc;
        try
        {
            inicioUtc = TimeZoneInfo.ConvertTimeToUtc(inicioCalendario, zona);
            fimUtc = TimeZoneInfo.ConvertTimeToUtc(fimCalendario, zona);
        }
        catch (ArgumentException)
        {
            return (ResultadoAgenda.PeriodoInvalido, []);
        }

        using var cmd = new MySqlCommand("""
            SELECT 'Reserva' AS tipo, r.inicio_utc, r.fim_utc,
                   COALESCE(NULLIF(TRIM(r.finalidade), ''), 'Reserva confirmada') AS descricao
            FROM reserva r
            WHERE r.espaco_id=@espaco AND r.status='Confirmada'
              AND r.inicio_utc<@fim AND r.fim_utc>@inicio
            UNION ALL
            SELECT 'Manutencao', m.inicio_utc, m.fim_utc,
                   CONCAT('Manutenção: ', m.motivo)
            FROM indisponibilidade_espaco m
            WHERE m.espaco_id=@espaco AND m.ativo=TRUE
              AND m.inicio_utc<@fim AND m.fim_utc>@inicio
            ORDER BY inicio_utc, fim_utc
            """, conexao);
        cmd.Parameters.AddWithValue("@espaco", espacoId);
        cmd.Parameters.AddWithValue("@inicio",
            DateTime.SpecifyKind(inicioUtc, DateTimeKind.Unspecified));
        cmd.Parameters.AddWithValue("@fim",
            DateTime.SpecifyKind(fimUtc, DateTimeKind.Unspecified));
        var itens = new List<OcupacaoEspacoItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            itens.Add(new OcupacaoEspacoItem(reader.GetString(0),
                ParaLocal(reader.GetDateTime(1), zona),
                ParaLocal(reader.GetDateTime(2), zona), reader.GetString(3)));
        return (ResultadoAgenda.Concluido, itens);
    }

    public async Task<ResultadoAgenda> CriarManutencaoAsync(
        long espacoId, ManutencaoEspacoEntrada entrada, long usuarioId,
        bool admin, CancellationToken ct)
    {
        if (entrada.InicioLocal is null || entrada.FimLocal is null ||
            entrada.FimLocal <= entrada.InicioLocal ||
            string.IsNullOrWhiteSpace(entrada.Motivo) ||
            entrada.Motivo.Trim().Length is < 3 or > 500 ||
            entrada.InicioLocal.Value.Kind != DateTimeKind.Unspecified ||
            entrada.FimLocal.Value.Kind != DateTimeKind.Unspecified)
            return ResultadoAgenda.PeriodoInvalido;

        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, fuso) = await VerificarEspacoAsync(
            conexao, transacao, espacoId, usuarioId, admin, true, ct);
        if (resultado != ResultadoAgenda.Concluido) return resultado;
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso!, out var zona))
            return ResultadoAgenda.FusoInvalido;

        var inicioLocal = entrada.InicioLocal.Value;
        var fimLocal = entrada.FimLocal.Value;
        if (zona.IsInvalidTime(inicioLocal) || zona.IsInvalidTime(fimLocal) ||
            zona.IsAmbiguousTime(inicioLocal) || zona.IsAmbiguousTime(fimLocal))
            return ResultadoAgenda.PeriodoInvalido;

        DateTime inicioUtc, fimUtc;
        try
        {
            inicioUtc = TimeZoneInfo.ConvertTimeToUtc(inicioLocal, zona);
            fimUtc = TimeZoneInfo.ConvertTimeToUtc(fimLocal, zona);
        }
        catch (ArgumentException)
        {
            return ResultadoAgenda.PeriodoInvalido;
        }
        if (fimUtc <= inicioUtc) return ResultadoAgenda.PeriodoInvalido;

        if (await ExisteReservaConfirmadaAsync(conexao, transacao,
                espacoId, inicioUtc, fimUtc, ct))
            return ResultadoAgenda.ConflitoReserva;

        using var cmd = new MySqlCommand("""
            INSERT INTO indisponibilidade_espaco
                (espaco_id, inicio_utc, fim_utc, motivo, ativo)
            VALUES (@espaco, @inicio, @fim, @motivo, TRUE)
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@espaco", espacoId);
        // DATETIME não armazena o fuso; o campo e o nome indicam UTC.
        cmd.Parameters.AddWithValue("@inicio",
            DateTime.SpecifyKind(inicioUtc, DateTimeKind.Unspecified));
        cmd.Parameters.AddWithValue("@fim",
            DateTime.SpecifyKind(fimUtc, DateTimeKind.Unspecified));
        cmd.Parameters.AddWithValue("@motivo", entrada.Motivo.Trim());
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            await UnidadeDoEspacoAsync(conexao, transacao, espacoId, ct),
            "CriarManutencao", "IndisponibilidadeEspaco", cmd.LastInsertedId,
            "Bloqueio de manutenção registrado.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoAgenda.Concluido;
    }

    public async Task<ResultadoAgenda> AlterarManutencaoAsync(
        long espacoId, long manutencaoId, bool ativo, long usuarioId,
        bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, _) = await VerificarEspacoAsync(
            conexao, transacao, espacoId, usuarioId, admin, true, ct);
        if (resultado != ResultadoAgenda.Concluido) return resultado;

        if (ativo)
        {
            using var consultar = new MySqlCommand("""
                SELECT inicio_utc, fim_utc
                FROM indisponibilidade_espaco
                WHERE id=@id AND espaco_id=@espaco FOR UPDATE
                """, conexao, transacao);
            consultar.Parameters.AddWithValue("@id", manutencaoId);
            consultar.Parameters.AddWithValue("@espaco", espacoId);
            DateTime inicio, fim;
            await using (var reader = await consultar.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct)) return ResultadoAgenda.NaoEncontrado;
                inicio = DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc);
                fim = DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc);
            }
            if (await ExisteReservaConfirmadaAsync(conexao, transacao,
                    espacoId, inicio, fim, ct))
                return ResultadoAgenda.ConflitoReserva;
        }

        using var cmd = new MySqlCommand("""
            UPDATE indisponibilidade_espaco SET ativo=@ativo
            WHERE id=@id AND espaco_id=@espaco
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@ativo", ativo);
        cmd.Parameters.AddWithValue("@id", manutencaoId);
        cmd.Parameters.AddWithValue("@espaco", espacoId);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0)
        {
            using var verificar = new MySqlCommand("""
                SELECT EXISTS (SELECT 1 FROM indisponibilidade_espaco
                    WHERE id=@id AND espaco_id=@espaco)
                """, conexao, transacao);
            verificar.Parameters.AddWithValue("@id", manutencaoId);
            verificar.Parameters.AddWithValue("@espaco", espacoId);
            if (Convert.ToInt32(await verificar.ExecuteScalarAsync(ct)) == 0)
                return ResultadoAgenda.NaoEncontrado;
        }

        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            await UnidadeDoEspacoAsync(conexao, transacao, espacoId, ct),
            ativo ? "ReativarManutencao" : "DesativarManutencao",
            "IndisponibilidadeEspaco", manutencaoId,
            "Situação do bloqueio alterada.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoAgenda.Concluido;
    }

    private static async Task<(ResultadoAgenda Resultado, string? Fuso)>
        VerificarEspacoAsync(MySqlConnection conexao, MySqlTransaction? transacao,
            long espacoId, long usuarioId, bool admin, bool bloquear,
            CancellationToken ct)
    {
        var sql = """
            SELECT u.fuso_horario,
              (@admin=TRUE OR EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.unidade_id=e.unidade_id
                  AND pu.papel IN ('FuncionarioUnidade','PresidenteClube')
              )) AS permitido
            FROM espaco e JOIN unidade u ON u.id=e.unidade_id
            WHERE e.id=@espaco
            """;
        if (bloquear) sql += " FOR UPDATE";
        using var cmd = new MySqlCommand(sql, conexao, transacao);
        cmd.Parameters.AddWithValue("@admin", admin);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@espaco", espacoId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return (ResultadoAgenda.NaoEncontrado, null);
        return reader.GetBoolean(1)
            ? (ResultadoAgenda.Concluido, reader.GetString(0))
            : (ResultadoAgenda.ForaDoEscopo, null);
    }

    private static async Task<long> UnidadeDoEspacoAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long espacoId, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT unidade_id FROM espaco WHERE id=@id
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@id", espacoId);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task<bool> ExisteReservaConfirmadaAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long espacoId, DateTime inicioUtc, DateTime fimUtc,
        CancellationToken ct)
    {
        using var verificar = new MySqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM reserva r
                WHERE r.espaco_id=@espaco AND r.status='Confirmada'
                  AND r.inicio_utc<@fim AND r.fim_utc>@inicio
            )
            """, conexao, transacao);
        verificar.Parameters.AddWithValue("@espaco", espacoId);
        verificar.Parameters.AddWithValue("@inicio",
            DateTime.SpecifyKind(inicioUtc, DateTimeKind.Unspecified));
        verificar.Parameters.AddWithValue("@fim",
            DateTime.SpecifyKind(fimUtc, DateTimeKind.Unspecified));
        return Convert.ToInt32(await verificar.ExecuteScalarAsync(ct)) == 1;
    }

    private static bool ValidarHorario(HorarioEspacoEntrada entrada,
        out TimeSpan inicio, out TimeSpan fim)
    {
        inicio = default;
        fim = default;
        if (entrada.DiaSemana is < 1 or > 7 ||
            !TimeOnly.TryParseExact(entrada.Inicio, "HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var i) ||
            !TimeOnly.TryParseExact(entrada.Fim, "HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
            return false;
        inicio = i.ToTimeSpan();
        fim = f.ToTimeSpan();
        return entrada.FimDiaSeguinte ? fim <= inicio : fim > inicio;
    }

    private static async Task<bool> ExisteConflitoSemanalAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long espacoId, long ignorarId, int dia, TimeSpan inicio,
        TimeSpan fim, bool diaSeguinte, CancellationToken ct)
    {
        var nova = Faixa(dia, inicio, fim, diaSeguinte);
        using var cmd = new MySqlCommand("""
            SELECT dia_semana, inicio, fim, fim_dia_seguinte
            FROM horario_espaco
            WHERE espaco_id=@espaco AND ativo=TRUE AND id<>@ignorar
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@espaco", espacoId);
        cmd.Parameters.AddWithValue("@ignorar", ignorarId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var existente = Faixa(Convert.ToInt32(reader.GetValue(0)),
                reader.GetTimeSpan(1), reader.GetTimeSpan(2),
                reader.GetBoolean(3));
            const int semana = 7 * 24 * 60;
            foreach (var deslocamento in new[] { -semana, 0, semana })
            {
                if (nova.Inicio < existente.Fim + deslocamento &&
                    existente.Inicio + deslocamento < nova.Fim)
                    return true;
            }
        }
        return false;
    }

    private static (int Inicio, int Fim) Faixa(
        int dia, TimeSpan inicio, TimeSpan fim, bool diaSeguinte)
    {
        var ini = (dia - 1) * 24 * 60 + (int)inicio.TotalMinutes;
        var duracao = (int)fim.TotalMinutes +
            (diaSeguinte ? 24 * 60 : 0) - (int)inicio.TotalMinutes;
        return (ini, ini + duracao);
    }

    private static HorarioEspacoItem LerHorario(MySqlDataReader reader) =>
        new(reader.GetInt64(0), reader.GetInt64(1), Convert.ToInt32(reader.GetValue(2)),
            FormatarHora(reader.GetTimeSpan(3)),
            FormatarHora(reader.GetTimeSpan(4)),
            reader.GetBoolean(5), reader.GetBoolean(6));

    private static string FormatarHora(TimeSpan hora) =>
        TimeOnly.FromTimeSpan(hora).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static DateTime ParaLocal(DateTime utc, TimeZoneInfo zona) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), zona);
}
