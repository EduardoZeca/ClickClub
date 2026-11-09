using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed record EquipeAtribuicaoApi(
    string Papel, long UnidadeId, string UnidadeNome);

public sealed record EquipeUsuarioApi(
    long Id, string Nome, string Login, bool Ativo,
    IReadOnlyList<EquipeAtribuicaoApi> Atribuicoes);

public sealed record ClubeAtribuidoApi(
    long Id, string Nome, string? Sigla,
    string Municipio, string Uf, string Status);

public sealed class EquipeCriacaoFormulario
{
    [Required(ErrorMessage = "Informe o nome."),
     StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(30)] public string? Telefone { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }

    [Required(ErrorMessage = "Informe o login."),
     StringLength(100, MinimumLength = 4)]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha."),
     StringLength(128, MinimumLength = 12)]
    [DataType(DataType.Password)]
    public string Senha { get; set; } = string.Empty;

    [Required, Compare(nameof(Senha), ErrorMessage = "As senhas não coincidem.")]
    [DataType(DataType.Password)]
    public string ConfirmarSenha { get; set; } = string.Empty;

    [Required, RegularExpression("^(FuncionarioUnidade|Porteiro|PresidenteClube|Tesoureiro)$")]
    public string Papel { get; set; } = string.Empty;

    [Range(1, long.MaxValue, ErrorMessage = "Selecione um clube.")]
    public long UnidadeId { get; set; }

    public IReadOnlyList<UnidadeApi> Unidades { get; set; } = [];

    public object ParaApi() => new
    {
        Nome, Telefone, Email, Login, Senha, Papel, UnidadeId
    };
}

public sealed class EquipeAtribuicaoFormulario
{
    [Range(1, long.MaxValue)] public long UsuarioId { get; set; }
    [Range(1, long.MaxValue)] public long UnidadeId { get; set; }
    [Required] public string Papel { get; set; } = string.Empty;
}

public sealed record EquipeDetalhesPagina(
    EquipeUsuarioApi Usuario, IReadOnlyList<UnidadeApi> Unidades);

public sealed class EquipeSenhaFormulario
{
    [Range(1, long.MaxValue)] public long UsuarioId { get; set; }

    [Required, StringLength(128, MinimumLength = 12)]
    [DataType(DataType.Password)]
    public string NovaSenha { get; set; } = string.Empty;

    [Required, Compare(nameof(NovaSenha), ErrorMessage = "As senhas não coincidem.")]
    [DataType(DataType.Password)]
    public string ConfirmarNovaSenha { get; set; } = string.Empty;
}

public static class NomesPapeis
{
    public static string Exibir(string papel) => papel switch
    {
        "FuncionarioUnidade" => "Funcionário da unidade",
        "PresidenteClube" => "Presidente do clube",
        "Tesoureiro" => "Tesoureiro",
        "Porteiro" => "Porteiro",
        _ => papel
    };
}
