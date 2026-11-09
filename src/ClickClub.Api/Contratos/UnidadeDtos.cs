using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class UnidadeEntrada
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 2)]
    public string Sigla { get; set; } = string.Empty;

    [Required, StringLength(255)]
    public string Endereco { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Bairro { get; set; }

    [Required, StringLength(120)]
    public string Municipio { get; set; } = string.Empty;

    [Required, RegularExpression("^[A-Za-z]{2}$")]
    public string Uf { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Telefone { get; set; }

    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }

    [Required, StringLength(64)]
    public string FusoHorario { get; set; } = "America/Sao_Paulo";
}

public sealed record UnidadeResposta(
    long Id,
    string Nome,
    string? Sigla,
    string Endereco,
    string? Bairro,
    string Municipio,
    string Uf,
    string? Telefone,
    string? Email,
    string FusoHorario,
    string Status);

public sealed record SituacaoUnidadeEntrada(bool Ativa);
