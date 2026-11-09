using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed class LoginViewModel
{
    [Required, StringLength(100)]
    public string Login { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Senha { get; set; } = string.Empty;
}

public sealed record PapelApi(string Papel, long? UnidadeId);
public sealed record UsuarioApi(long Id, string Nome, string Login,
    IReadOnlyList<PapelApi> Papeis);
public sealed record LoginRespostaApi(string Token, DateTime ExpiraEmUtc,
    UsuarioApi Usuario);

public sealed record UnidadeApi(long Id, string Nome, string? Sigla,
    string Endereco, string? Bairro, string Municipio, string Uf,
    string? Telefone, string? Email, string FusoHorario, string Status);

public sealed class UnidadeFormulario
{
    public long Id { get; set; }

    [Required(ErrorMessage = "Informe o nome."), StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a sigla."), StringLength(20, MinimumLength = 2)]
    public string Sigla { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o endereço."), StringLength(255)]
    public string Endereco { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Bairro { get; set; }

    [Required(ErrorMessage = "Informe o município."), StringLength(120)]
    public string Municipio { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a UF."), RegularExpression("^[A-Za-z]{2}$",
        ErrorMessage = "Informe duas letras para a UF.")]
    public string Uf { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Telefone { get; set; }

    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }

    [Required, StringLength(64)]
    public string FusoHorario { get; set; } = "America/Sao_Paulo";

    public static UnidadeFormulario De(UnidadeApi item) => new()
    {
        Id = item.Id, Nome = item.Nome, Sigla = item.Sigla ?? string.Empty,
        Endereco = item.Endereco, Bairro = item.Bairro,
        Municipio = item.Municipio, Uf = item.Uf,
        Telefone = item.Telefone, Email = item.Email,
        FusoHorario = item.FusoHorario
    };
}

public sealed record CategoriaApi(long Id, string Nome, bool Ativa);

public sealed class CategoriaFormulario
{
    public long Id { get; set; }

    [Required(ErrorMessage = "Informe o nome."), StringLength(100, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;
}
