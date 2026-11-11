namespace ClickClub.Api.Contratos;

public sealed record ValidacaoQrEntrada(
    long UnidadeId,
    string? CodigoQr,
    string TipoAcesso,
    long? ReservaId);

public sealed record ValidacaoQrResposta(
    long RegistroId,
    bool Permitido,
    string Resultado,
    string MotivoCodigo,
    string Mensagem,
    string? NomePessoa,
    DateTime ValidadoEmUtc);

public sealed record ValidacaoConvidadoEntrada(
    long UnidadeId, long ReservaId,
    string? IdentificacaoMinima, bool IdentidadeConferida);

public sealed record ValidacaoConvidadoResposta(
    long RegistroId, bool Permitido, string Resultado,
    string MotivoCodigo, string Mensagem,
    string? NomeConvidado, DateTime ValidadoEmUtc);

public sealed record UnidadePortariaResposta(
    long Id, string Nome, string? Sigla);

public sealed record RegistroPortariaResposta(
    long Id, DateTime DataHoraUtc,
    string TipoAcesso, string Resultado,
    string MotivoCodigo, string? NomePessoa);
