using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed record CarteirinhaApi(
    string CodigoQr, string IdentificadorPublico,
    DateTime EmitidaEmUtc, DateTime ValidaAteUtc,
    int Versao, bool EmitidaAgora);

public sealed class CarteirinhaPaginaViewModel
{
    public MinhaAssociacaoApi? Associacao { get; set; }
    public CarteirinhaApi? Credencial { get; set; }
    public string? ImagemQrBase64 { get; set; }
    public string? Erro { get; set; }
    public bool CredencialValida => Credencial is not null &&
        Credencial.ValidaAteUtc > DateTime.UtcNow;
}

public sealed record UnidadePortariaApi(long Id, string Nome, string? Sigla);

public sealed record RegistroPortariaApi(
    long Id, DateTime DataHoraUtc, string TipoAcesso,
    string Resultado, string MotivoCodigo, string? NomePessoa);

public sealed record ValidacaoQrPortariaApi(
    long RegistroId, bool Permitido, string Resultado,
    string MotivoCodigo, string Mensagem, string? NomePessoa,
    DateTime ValidadoEmUtc);

public sealed record ValidacaoConvidadoPortariaApi(
    long RegistroId, bool Permitido, string Resultado,
    string MotivoCodigo, string Mensagem, string? NomeConvidado,
    DateTime ValidadoEmUtc);

public sealed record ResultadoPortariaViewModel(
    long UnidadeId, long RegistroId, bool Permitido, string Mensagem,
    string MotivoCodigo, string? NomeIdentificado,
    DateTime ValidadoEmUtc);

public sealed class PortariaPaginaViewModel
{
    public IReadOnlyList<UnidadePortariaApi> Unidades { get; set; } = [];
    public IReadOnlyList<RegistroPortariaApi> Historico { get; set; } = [];
    public long UnidadeSelecionadaId { get; set; }
    public ResultadoPortariaViewModel? Resultado { get; set; }
    public string? Erro { get; set; }
}

public sealed class ValidacaoQrPortariaFormulario
{
    [Range(1, long.MaxValue)] public long UnidadeId { get; set; }
    [Required(ErrorMessage = "Informe o conteúdo do QR Code.")]
    [StringLength(1024)] public string CodigoQr { get; set; } = string.Empty;
    [Required] public string TipoAcesso { get; set; } = "EntradaGeral";
    public long? ReservaId { get; set; }
}

public sealed class ValidacaoConvidadoPortariaFormulario
{
    [Range(1, long.MaxValue)] public long UnidadeId { get; set; }
    [Range(1, long.MaxValue)] public long ReservaId { get; set; }
    [Required, StringLength(60, MinimumLength = 3)]
    public string IdentificacaoMinima { get; set; } = string.Empty;
    public bool IdentidadeConferida { get; set; }
}

public sealed class ValidacaoVisitanteEventoPortariaFormulario
{
    [Range(1, long.MaxValue)] public long UnidadeId { get; set; }
    [Range(1, long.MaxValue)] public long EventoId { get; set; }
    [Required, StringLength(60, MinimumLength = 3)]
    public string IdentificacaoMinima { get; set; } = string.Empty;
    public bool IdentidadeConferida { get; set; }
}
