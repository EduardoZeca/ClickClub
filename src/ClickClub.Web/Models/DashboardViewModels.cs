namespace ClickClub.Web.Models;

public sealed record DashboardFinanceiroApi(
    long FaturasQuitadas, decimal ValorQuitado,
    long FaturasEmAberto, decimal ValorEmAberto,
    long FaturasVencidas, decimal ValorVencido,
    long FaturasEmPreparacao);

public sealed record DashboardUnidadeApi(
    long UnidadeId, string UnidadeNome,
    long EspacosAtivos, long EspacosOcupados, long ReservasConfirmadas,
    long InscricoesTurmasConfirmadas, long InscricoesTurmasEmEspera,
    long InscricoesEventosConfirmadas,
    DashboardFinanceiroApi? Financeiro);

public sealed record DashboardApi(
    DateOnly Inicio, DateOnly Fim,
    IReadOnlyList<DashboardUnidadeApi> Unidades);

public sealed class DashboardPagina
{
    public DateOnly Inicio { get; init; }
    public DateOnly Fim { get; init; }
    public long? UnidadeId { get; init; }
    public DashboardApi? Resumo { get; set; }
    public string? Erro { get; set; }
}
