using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class AvisoEntrada
{
    public long? UnidadeId { get; set; }

    [Required, StringLength(150, MinimumLength = 2)]
    public string Titulo { get; set; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 2)]
    public string Corpo { get; set; } = string.Empty;

    [Required] public DateTime? InicioUtc { get; set; }
    [Required] public DateTime? FimUtc { get; set; }
}

public sealed class SituacaoAvisoEntrada
{
    public bool Ativo { get; set; }
}

public sealed record AvisoItem(
    long Id, long? UnidadeId, string? Clube, string Titulo,
    string Corpo, DateTime InicioUtc, DateTime FimUtc,
    bool Ativo, string Autor);

public sealed record ClubeAvisoOpcao(long Id, string Nome);

public enum ResultadoAviso
{
    Concluido, NaoEncontrado, ForaDoEscopo,
    UnidadeInativa, UnidadeImutavel, PeriodoInvalido, DadosInvalidos
}
