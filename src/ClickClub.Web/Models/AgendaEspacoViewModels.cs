using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed record HorarioEspacoApi(
    long Id, long EspacoId, int DiaSemana, string Inicio,
    string Fim, bool FimDiaSeguinte, bool Ativo);

public sealed record ManutencaoEspacoApi(
    long Id, long EspacoId, DateTime InicioLocal,
    DateTime FimLocal, string Motivo, bool Ativo);

public sealed record OcupacaoEspacoApi(
    string Tipo, DateTime InicioLocal, DateTime FimLocal,
    string Descricao);

public sealed class HorarioEspacoFormulario
{
    [Range(1, long.MaxValue)] public long EspacoId { get; set; }
    [Range(1, 7)] public int DiaSemana { get; set; }
    [Required, RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$")]
    public string Inicio { get; set; } = string.Empty;
    [Required, RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$")]
    public string Fim { get; set; } = string.Empty;
    public bool FimDiaSeguinte { get; set; }
}

public sealed class ManutencaoEspacoFormulario
{
    [Range(1, long.MaxValue)] public long EspacoId { get; set; }
    [Required] public DateTime? InicioLocal { get; set; }
    [Required] public DateTime? FimLocal { get; set; }
    [Required, StringLength(500, MinimumLength = 3)]
    public string Motivo { get; set; } = string.Empty;
}

public sealed record AgendaEspacoPagina(
    EspacoApi Espaco,
    IReadOnlyList<HorarioEspacoApi> Horarios,
    IReadOnlyList<ManutencaoEspacoApi> Manutencoes,
    int Ano, int Mes,
    IReadOnlyList<OcupacaoEspacoApi> Ocupacoes);
