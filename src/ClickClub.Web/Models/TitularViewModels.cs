using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed record TitularResumoApi(
    long Id, string Nome, string CpfFinal, string Matricula,
    string ClubeOrigem, string Categoria, string TipoVinculo, string Status);

public sealed record TitularDetalheApi(
    long Id, long PessoaId, string Nome, string Cpf,
    DateTime? DataNascimento, string? Endereco, string? Bairro,
    string? Cidade, string? Uf, string? Sexo, string? EstadoCivil,
    string? Telefone, string? Email, string Matricula,
    long UnidadeOrigemId, long CategoriaId, long TipoVinculoId,
    DateTime DataInicio, string Status, IReadOnlyList<string> Interesses);

public sealed record UnidadeTitularOpcaoApi(long Id, string Nome, string Status);
public sealed record CategoriaTitularOpcaoApi(long Id, string Nome, bool Ativa);
public sealed record TipoVinculoTitularOpcaoApi(
    long Id, string Nome, bool DireitoVoto, bool Ativo);

public sealed record TitularOpcoesApi(
    IReadOnlyList<UnidadeTitularOpcaoApi> Unidades,
    IReadOnlyList<CategoriaTitularOpcaoApi> Categorias,
    IReadOnlyList<TipoVinculoTitularOpcaoApi> TiposVinculo);

public sealed record ErroApi(string Erro);

public sealed class TitularFormulario
{
    public long Id { get; set; }

    [Required(ErrorMessage = "Informe o nome."), StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o CPF."),
     RegularExpression("^[0-9]{11}$", ErrorMessage = "Informe 11 dígitos, sem pontuação.")]
    public string Cpf { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? DataNascimento { get; set; }

    [StringLength(255)] public string? Endereco { get; set; }
    [StringLength(120)] public string? Bairro { get; set; }
    [StringLength(120)] public string? Cidade { get; set; }
    [RegularExpression("^([A-Za-z]{2})?$", ErrorMessage = "Informe uma UF com duas letras.")]
    public string? Uf { get; set; }
    [StringLength(40)] public string? Sexo { get; set; }
    [StringLength(40)] public string? EstadoCivil { get; set; }
    [StringLength(30)] public string? Telefone { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }

    [Required(ErrorMessage = "Informe a matrícula."),
     StringLength(30, MinimumLength = 2)]
    public string Matricula { get; set; } = string.Empty;

    [Range(1, long.MaxValue, ErrorMessage = "Selecione um clube de origem.")]
    public long UnidadeOrigemId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Selecione a categoria.")]
    public long CategoriaId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Selecione o tipo de vínculo.")]
    public long TipoVinculoId { get; set; }

    [Required, DataType(DataType.Date)]
    public DateTime DataInicio { get; set; } = DateTime.Today;

    [StringLength(1500)]
    public string? InteressesTexto { get; set; }

    public TitularOpcoesApi? Opcoes { get; set; }

    public object ParaApi() => new
    {
        Nome, Cpf, DataNascimento, Endereco, Bairro, Cidade, Uf, Sexo,
        EstadoCivil, Telefone, Email, Matricula, UnidadeOrigemId,
        CategoriaId, TipoVinculoId, DataInicio,
        Interesses = (InteressesTexto ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.TrimEntries |
                              StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
    };

    public static TitularFormulario De(TitularDetalheApi item) => new()
    {
        Id = item.Id,
        Nome = item.Nome,
        Cpf = item.Cpf,
        DataNascimento = item.DataNascimento,
        Endereco = item.Endereco,
        Bairro = item.Bairro,
        Cidade = item.Cidade,
        Uf = item.Uf,
        Sexo = item.Sexo,
        EstadoCivil = item.EstadoCivil,
        Telefone = item.Telefone,
        Email = item.Email,
        Matricula = item.Matricula,
        UnidadeOrigemId = item.UnidadeOrigemId,
        CategoriaId = item.CategoriaId,
        TipoVinculoId = item.TipoVinculoId,
        DataInicio = item.DataInicio,
        InteressesTexto = string.Join(", ", item.Interesses)
    };
}
