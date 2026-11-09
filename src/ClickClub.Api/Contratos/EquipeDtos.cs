using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class EquipeCriacaoEntrada
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(30)] public string? Telefone { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }

    [Required, StringLength(100, MinimumLength = 4)]
    public string Login { get; set; } = string.Empty;

    [Required, MinLength(12), MaxLength(128)]
    public string Senha { get; set; } = string.Empty;

    [Required]
    public string Papel { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long UnidadeId { get; set; }
}

public sealed class EquipeAtribuicaoEntrada
{
    [Required] public string Papel { get; set; } = string.Empty;
    [Range(1, long.MaxValue)] public long UnidadeId { get; set; }
}

public sealed record EquipeAtribuicaoResposta(
    string Papel, long UnidadeId, string UnidadeNome);

public sealed record EquipeUsuarioResposta(
    long Id, string Nome, string Login, bool Ativo,
    IReadOnlyList<EquipeAtribuicaoResposta> Atribuicoes);

public sealed record EquipeSituacaoEntrada(bool Ativo);

public sealed class EquipeSenhaEntrada
{
    [Required, MinLength(12), MaxLength(128)]
    public string NovaSenha { get; set; } = string.Empty;
}

public sealed record ClubeAtribuidoResposta(
    long Id, string Nome, string? Sigla,
    string Municipio, string Uf, string Status);
