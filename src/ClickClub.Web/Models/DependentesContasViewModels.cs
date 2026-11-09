using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed record DependenteResumoApi(
    long Id, long PessoaId, long AssociacaoId,
    string Nome, string? CpfFinal, DateTime? DataNascimento,
    bool Ativo, long? UsuarioId, string? Login);

public sealed record DependenteDetalheApi(
    long Id, long PessoaId, long AssociacaoId,
    string Nome, string? Cpf, DateTime? DataNascimento,
    string? Endereco, string? Bairro, string? Cidade,
    string? Uf, string? Sexo, string? EstadoCivil,
    string? Telefone, string? Email, bool Ativo);

public sealed class DependenteFormulario
{
    public long Id { get; set; }

    [Range(1, long.MaxValue)]
    public long AssociacaoId { get; set; }

    [Required(ErrorMessage = "Informe o nome."), StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [RegularExpression("^([0-9]{11})?$", ErrorMessage = "Informe 11 dígitos, sem pontuação.")]
    public string? Cpf { get; set; }

    [Required(ErrorMessage = "Informe a data de nascimento."), DataType(DataType.Date)]
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

    public static DependenteFormulario De(DependenteDetalheApi item) => new()
    {
        Id = item.Id,
        AssociacaoId = item.AssociacaoId,
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
        Email = item.Email
    };

    public object ParaApi() => new
    {
        AssociacaoId, Nome, Cpf, DataNascimento, Endereco, Bairro,
        Cidade, Uf, Sexo, EstadoCivil, Telefone, Email
    };
}

public sealed record ContaAssociativaApi(
    long UsuarioId, long PessoaId, string Login, bool Ativo, string Papel);

public sealed class ContaCriacaoFormulario
{
    [Range(1, long.MaxValue)] public long PessoaId { get; set; }
    [Range(1, long.MaxValue)] public long AssociacaoId { get; set; }
    [Required, RegularExpression("^(Titular|Dependente)$")]
    public string Papel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o login."), StringLength(100, MinimumLength = 4)]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha."), StringLength(128, MinimumLength = 12)]
    [DataType(DataType.Password)]
    public string Senha { get; set; } = string.Empty;

    [Required, Compare(nameof(Senha), ErrorMessage = "As senhas precisam coincidir.")]
    [DataType(DataType.Password)]
    public string ConfirmarSenha { get; set; } = string.Empty;

    public object ParaApi() => new { PessoaId, Papel, Login, Senha };
}

public sealed class ContaSenhaFormulario
{
    public long UsuarioId { get; set; }
    public long PessoaId { get; set; }
    public long AssociacaoId { get; set; }
    public string Papel { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    [DataType(DataType.Password)]
    public string NovaSenha { get; set; } = string.Empty;

    [Required, Compare(nameof(NovaSenha), ErrorMessage = "As senhas precisam coincidir.")]
    [DataType(DataType.Password)]
    public string ConfirmarNovaSenha { get; set; } = string.Empty;
}

public sealed record MinhaAssociacaoApi(
    string Nome, string Perfil, string Matricula,
    string ClubeOrigem, string Categoria, string Status);
