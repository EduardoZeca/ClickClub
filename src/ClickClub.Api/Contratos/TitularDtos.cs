using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class TitularEntrada
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [Required, RegularExpression("^[0-9]{11}$", ErrorMessage = "CPF deve ter 11 dígitos.")]
    public string Cpf { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? DataNascimento { get; set; }

    [StringLength(255)] public string? Endereco { get; set; }
    [StringLength(120)] public string? Bairro { get; set; }
    [StringLength(120)] public string? Cidade { get; set; }
    [RegularExpression("^([A-Za-z]{2})?$", ErrorMessage = "UF deve ter duas letras.")]
    public string? Uf { get; set; }
    [StringLength(40)] public string? Sexo { get; set; }
    [StringLength(40)] public string? EstadoCivil { get; set; }
    [StringLength(30)] public string? Telefone { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }

    [Required, StringLength(30, MinimumLength = 2)]
    public string Matricula { get; set; } = string.Empty;

    [Range(1, long.MaxValue)] public long UnidadeOrigemId { get; set; }
    [Range(1, long.MaxValue)] public long CategoriaId { get; set; }
    [Range(1, long.MaxValue)] public long TipoVinculoId { get; set; }

    [Required, DataType(DataType.Date)]
    public DateTime DataInicio { get; set; }

    public List<string> Interesses { get; set; } = [];
}

public sealed record TitularResumo(
    long Id, string Nome, string CpfFinal, string Matricula,
    string ClubeOrigem, string Categoria, string TipoVinculo, string Status);

public sealed record TitularDetalhe(
    long Id, long PessoaId, string Nome, string Cpf,
    DateTime? DataNascimento, string? Endereco, string? Bairro,
    string? Cidade, string? Uf, string? Sexo, string? EstadoCivil,
    string? Telefone, string? Email, string Matricula,
    long UnidadeOrigemId, long CategoriaId, long TipoVinculoId,
    DateTime DataInicio, string Status, IReadOnlyList<string> Interesses);

public sealed record UnidadeCadastroOpcao(long Id, string Nome, string Status);
public sealed record CategoriaCadastroOpcao(long Id, string Nome, bool Ativa);
public sealed record TipoVinculoCadastroOpcao(
    long Id, string Nome, bool DireitoVoto, bool Ativo);

public sealed record TitularOpcoes(
    IReadOnlyList<UnidadeCadastroOpcao> Unidades,
    IReadOnlyList<CategoriaCadastroOpcao> Categorias,
    IReadOnlyList<TipoVinculoCadastroOpcao> TiposVinculo);

public sealed record TitularSituacaoEntrada(
    [Required, RegularExpression("^(Ativa|Suspensa)$")] string Status);
