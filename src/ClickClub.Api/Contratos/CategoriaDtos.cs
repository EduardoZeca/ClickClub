using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class CategoriaEntrada
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;
}

public sealed record CategoriaResposta(long Id, string Nome, bool Ativa);
public sealed record SituacaoCategoriaEntrada(bool Ativa);
