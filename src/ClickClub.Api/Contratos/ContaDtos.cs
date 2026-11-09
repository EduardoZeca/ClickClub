using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class ContaCriacaoEntrada
{
    [Range(1, long.MaxValue)]
    public long PessoaId { get; set; }

    [Required, StringLength(100, MinimumLength = 4)]
    public string Login { get; set; } = string.Empty;

    [Required, MinLength(12), MaxLength(128)]
    public string Senha { get; set; } = string.Empty;

    [Required, RegularExpression("^(Titular|Dependente)$")]
    public string Papel { get; set; } = string.Empty;
}

public sealed record ContaAdministrativaResposta(
    long UsuarioId, long PessoaId, string Login, bool Ativo, string Papel);

public sealed record SituacaoContaEntrada(bool Ativo);

public sealed class ContaSenhaEntrada
{
    [Required, MinLength(12), MaxLength(128)]
    public string NovaSenha { get; set; } = string.Empty;
}

public sealed record MinhaAssociacaoResposta(
    string Nome, string Perfil, string Matricula,
    string ClubeOrigem, string Categoria, string Status);
