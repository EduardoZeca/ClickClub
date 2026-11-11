using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class VisitanteEventoEntrada
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;
    [Required, StringLength(60, MinimumLength = 3)]
    public string IdentificacaoMinima { get; set; } = string.Empty;
}

public sealed class SituacaoVisitanteEventoEntrada
{
    [Required, RegularExpression("^(Autorizado|Revogado)$")]
    public string Situacao { get; set; } = string.Empty;
}

public sealed record VisitanteEventoItem(
    long Id, long EventoId, string Nome,
    string IdentificacaoMinima, string Situacao);

public sealed record ValidacaoVisitanteEventoEntrada(
    long UnidadeId, long EventoId,
    string? IdentificacaoMinima, bool IdentidadeConferida);

public sealed record ValidacaoVisitanteEventoResposta(
    long RegistroId, bool Permitido, string Resultado,
    string MotivoCodigo, string Mensagem, string? NomeConvidado,
    DateTime ValidadoEmUtc);

public enum ResultadoVisitanteEvento
{
    Concluido, EventoNaoEncontrado, ForaDoEscopo,
    EventoIndisponivel, PublicoNaoPermitido,
    VisitanteNaoEncontrado, Duplicado, CapacidadeEsgotada
}
