namespace ClickClub.Api.Contratos;

// O código QR é uma credencial assinada, independente do token de login.
// Não incluir CPF, matrícula, nome ou informações financeiras no código.
public sealed record CarteirinhaResposta(
    string CodigoQr,
    string IdentificadorPublico,
    DateTime EmitidaEmUtc,
    DateTime ValidaAteUtc,
    int Versao,
    bool EmitidaAgora);

// Contrato interno do conteúdo assinado (não é um DTO de pessoa).
public sealed record CarteirinhaPayload(
    int Versao,
    string IdentificadorPublico,
    long EmitidaEmUnix,
    long ValidaAteUnix);
