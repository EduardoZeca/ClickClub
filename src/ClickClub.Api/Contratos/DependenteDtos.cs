using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class DependenteEntrada
{
    [Range(1, long.MaxValue)]
    public long AssociacaoId { get; set; }

    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [RegularExpression("^([0-9]{11})?$", ErrorMessage = "CPF deve ter 11 dígitos.")]
    public string? Cpf { get; set; }

    [Required, DataType(DataType.Date)]
    public DateTime DataNascimento { get; set; }

    [StringLength(255)] public string? Endereco { get; set; }
    [StringLength(120)] public string? Bairro { get; set; }
    [StringLength(120)] public string? Cidade { get; set; }
    [RegularExpression("^([A-Za-z]{2})?$", ErrorMessage = "UF deve ter duas letras.")]
    public string? Uf { get; set; }
    [StringLength(40)] public string? Sexo { get; set; }
    [StringLength(40)] public string? EstadoCivil { get; set; }
    [StringLength(30)] public string? Telefone { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
}

public sealed record DependenteResumo(
    long Id, long PessoaId, long AssociacaoId,
    string Nome, string? CpfFinal, DateTime? DataNascimento,
    bool Ativo, long? UsuarioId, string? Login);

public sealed record DependenteDetalhe(
    long Id, long PessoaId, long AssociacaoId,
    string Nome, string? Cpf, DateTime? DataNascimento,
    string? Endereco, string? Bairro, string? Cidade,
    string? Uf, string? Sexo, string? EstadoCivil,
    string? Telefone, string? Email, bool Ativo);

public sealed record SituacaoDependenteEntrada(bool Ativo);
