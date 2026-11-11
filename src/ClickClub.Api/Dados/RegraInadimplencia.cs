namespace ClickClub.Api.Dados;

// Todas as datas financeiras são avaliadas no fuso do clube de origem.
// O atraso é contado por dias de calendário, não por blocos de 24 horas.
public static class RegraInadimplencia
{
    public const int DiasTolerancia = 10;
    public const decimal PercentualMulta = 0.02m;

    public static DateTime DataLocal(DateTime agoraUtc, string fusoHorario)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fusoHorario, out var zona))
            throw new InvalidOperationException(
                "Fuso horário inválido no clube de origem da associação.");

        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(agoraUtc, DateTimeKind.Utc), zona).Date;
    }

    public static int DiasAtraso(DateTime vencimento, DateTime dataLocal) =>
        Math.Max(0, (dataLocal.Date - vencimento.Date).Days);

    public static bool Bloqueia(string status, decimal valorTotal,
        DateTime vencimento, DateTime dataLocal) =>
        status == "EmAberto" && valorTotal > 0 &&
        DiasAtraso(vencimento, dataLocal) > DiasTolerancia;

    public static decimal MultaPendente(string status, decimal valorTotal,
        DateTime vencimento, DateTime dataLocal, bool multaJaLancada) =>
        Bloqueia(status, valorTotal, vencimento, dataLocal) && !multaJaLancada
            ? decimal.Round(valorTotal * PercentualMulta, 2,
                MidpointRounding.AwayFromZero)
            : 0m;
}
