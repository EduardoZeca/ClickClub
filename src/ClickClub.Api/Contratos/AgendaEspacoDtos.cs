using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class HorarioEspacoEntrada
{
    [Range(1, 7)] public int DiaSemana { get; set; }
    [Required, RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$")]
    public string Inicio { get; set; } = string.Empty;
    [Required, RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$")]
    public string Fim { get; set; } = string.Empty;
    public bool FimDiaSeguinte { get; set; }
}

public sealed record HorarioEspacoItem(
    long Id, long EspacoId, int DiaSemana, string Inicio,
    string Fim, bool FimDiaSeguinte, bool Ativo);

public sealed class ManutencaoEspacoEntrada
{
    [Required] public DateTime? InicioLocal { get; set; }
    [Required] public DateTime? FimLocal { get; set; }
    [Required, StringLength(500, MinimumLength = 3)]
    public string Motivo { get; set; } = string.Empty;
}

public sealed record ManutencaoEspacoItem(
    long Id, long EspacoId, DateTime InicioLocal,
    DateTime FimLocal, string Motivo, bool Ativo);

public sealed record OcupacaoEspacoItem(
    string Tipo, DateTime InicioLocal, DateTime FimLocal,
    string Descricao);

public sealed class SituacaoAgendaEntrada
{
    public bool Ativo { get; set; }
}

public enum ResultadoAgenda
{
    Concluido, NaoEncontrado, ForaDoEscopo,
    PeriodoInvalido, ConflitoHorario, ConflitoReserva, FusoInvalido
}
