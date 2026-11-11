namespace ClickClub.Api.Contratos;

public sealed record DashboardFinanceiroResumo(
    long FaturasQuitadas, decimal ValorQuitado,
    long FaturasEmAberto, decimal ValorEmAberto,
    long FaturasVencidas, decimal ValorVencido,
    long FaturasEmPreparacao);

public sealed record DashboardUnidadeResumo(
    long UnidadeId, string UnidadeNome,
    long EspacosAtivos, long EspacosOcupados, long ReservasConfirmadas,
    long InscricoesTurmasConfirmadas, long InscricoesTurmasEmEspera,
    long InscricoesEventosConfirmadas,
    DashboardFinanceiroResumo? Financeiro);

public sealed record DashboardResposta(
    DateOnly Inicio, DateOnly Fim,
    IReadOnlyList<DashboardUnidadeResumo> Unidades);
