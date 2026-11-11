using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed record AvisoApi(
    long Id, long? UnidadeId, string? Clube,
    string Titulo, string Corpo, DateTime InicioUtc,
    DateTime FimUtc, bool Ativo, string Autor);

public sealed record ClubeAvisoApi(long Id, string Nome);

public sealed class AvisoFormulario
{
    public long Id { get; set; }
    public long? UnidadeId { get; set; }

    [Required, StringLength(150, MinimumLength = 2)]
    public string Titulo { get; set; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 2)]
    public string Corpo { get; set; } = string.Empty;

    [Required] public DateTime? InicioUtc { get; set; }
    [Required] public DateTime? FimUtc { get; set; }

    public IReadOnlyList<ClubeAvisoApi> Clubes { get; set; } = [];

    public static AvisoFormulario De(AvisoApi aviso) => new()
    {
        Id = aviso.Id, UnidadeId = aviso.UnidadeId,
        Titulo = aviso.Titulo, Corpo = aviso.Corpo,
        InicioUtc = aviso.InicioUtc, FimUtc = aviso.FimUtc
    };
}
