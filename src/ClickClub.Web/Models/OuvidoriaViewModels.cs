using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed record ChamadoOuvidoriaApi(
    long Id, long PessoaId, string Solicitante,
    long? UnidadeId, string? Clube,
    string Tipo, string Categoria, string Assunto, string Descricao,
    string Status, DateTime CriadoEmUtc, DateTime AtualizadoEmUtc,
    DateTime? EncerradoEmUtc);

public sealed record RespostaOuvidoriaApi(
    long Id, string Autor, string Conteudo, DateTime DataHoraUtc);

public sealed record ChamadoOuvidoriaDetalheApi(
    ChamadoOuvidoriaApi Chamado,
    IReadOnlyList<RespostaOuvidoriaApi> Respostas);

public sealed record ClubeOuvidoriaApi(long Id, string Nome);

public sealed class ChamadoOuvidoriaFormulario
{
    public long? UnidadeId { get; set; }

    [Required, RegularExpression("^(Solicitacao|Reclamacao)$")]
    public string Tipo { get; set; } = "Solicitacao";

    [Required, StringLength(80, MinimumLength = 2)]
    public string Categoria { get; set; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 2)]
    public string Assunto { get; set; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 2)]
    public string Descricao { get; set; } = string.Empty;

    public IReadOnlyList<ClubeOuvidoriaApi> Clubes { get; set; } = [];
}

public sealed class RespostaOuvidoriaFormulario
{
    [Range(1, long.MaxValue)] public long Id { get; set; }
    [Required, StringLength(4000, MinimumLength = 2)]
    public string Conteudo { get; set; } = string.Empty;
}
