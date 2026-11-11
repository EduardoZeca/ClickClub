using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class ChamadoOuvidoriaEntrada
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
}

public sealed class RespostaOuvidoriaEntrada
{
    [Required, StringLength(4000, MinimumLength = 2)]
    public string Conteudo { get; set; } = string.Empty;
}

public sealed record ChamadoOuvidoriaItem(
    long Id, long PessoaId, string Solicitante,
    long? UnidadeId, string? Clube,
    string Tipo, string Categoria, string Assunto, string Descricao,
    string Status, DateTime CriadoEmUtc, DateTime AtualizadoEmUtc,
    DateTime? EncerradoEmUtc);

public sealed record RespostaOuvidoriaItem(
    long Id, string Autor, string Conteudo, DateTime DataHoraUtc);

public sealed record ChamadoOuvidoriaDetalhe(
    ChamadoOuvidoriaItem Chamado, List<RespostaOuvidoriaItem> Respostas);

public sealed record ClubeOuvidoriaOpcao(long Id, string Nome);

public enum ResultadoOuvidoria
{
    Concluido, NaoEncontrado, ForaDoEscopo, UnidadeInativa,
    SemContaAtiva, JaEncerrado, SemResposta, DadosInvalidos
}
