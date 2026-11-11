using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class TransferenciaLocacaoEntrada
{
    [Required]
    public DateTime? CompetenciaConfirmada { get; set; }
}

public sealed record CobrancaLocacaoResposta(
    long ReservaId, string ClubePrestador, string Espaco,
    DateTime InicioLocal, string SituacaoReserva, string SituacaoCobranca,
    decimal ValorOriginal, decimal AcrescimoTransferencia,
    decimal TotalTransferencia, DateTime? CompetenciaDestino,
    DateTime? VencimentoDestino, long? FaturaDestinoId,
    bool PodeDecidir, string? MotivoIndisponibilidade);

public sealed record DecisaoLocacaoResposta(
    long ReservaId, string SituacaoCobranca, bool JaRegistrada,
    decimal ValorOriginal, decimal AcrescimoTransferencia,
    DateTime? CompetenciaDestino, DateTime? VencimentoDestino,
    long? FaturaDestinoId);
