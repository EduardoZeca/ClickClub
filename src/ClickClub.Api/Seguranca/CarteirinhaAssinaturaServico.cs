using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClickClub.Api.Contratos;
using Microsoft.AspNetCore.WebUtilities;

namespace ClickClub.Api.Seguranca;

public sealed class CarteirinhaAssinaturaServico
{
    private const string Prefixo = "CC1";
    private readonly byte[] _chavePrivadaPkcs8;

    public CarteirinhaAssinaturaServico(IConfiguration configuration)
    {
        var valor = configuration["Carteirinha:ChavePrivadaPkcs8Base64"];
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException(
                "Configure Carteirinha:ChavePrivadaPkcs8Base64 em User Secrets da API.");

        try
        {
            _chavePrivadaPkcs8 = Convert.FromBase64String(valor);
            using var chave = AbrirChave();
            if (chave.KeySize != 256 ||
                chave.ExportParameters(false).Curve.Oid.Value !=
                    ECCurve.NamedCurves.nistP256.Oid.Value)
                throw new InvalidOperationException(
                    "A chave da carteirinha deve usar ECDSA P-256.");
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "A chave da carteirinha deve estar em Base64 PKCS#8 válido.");
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(
                "A chave da carteirinha não é uma chave privada ECDSA PKCS#8 válida.");
        }
    }

    public string Assinar(CarteirinhaPayload payload)
    {
        var dados = WebEncoders.Base64UrlEncode(
            JsonSerializer.SerializeToUtf8Bytes(payload));
        var conteudo = $"{Prefixo}.{dados}";

        using var chave = AbrirChave();
        var assinatura = chave.SignData(
            Encoding.UTF8.GetBytes(conteudo), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{conteudo}.{WebEncoders.Base64UrlEncode(assinatura)}";
    }

    // Confere somente autenticidade criptográfica e formato. A Etapa 2
    // também verificará banco, revogação, vínculo, faturas e escopo da unidade.
    public bool TentarVerificarAssinatura(
        string? codigo, out CarteirinhaPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 1024)
            return false;

        var partes = codigo.Split('.');
        if (partes.Length != 3 || partes[0] != Prefixo)
            return false;

        try
        {
            var assinatura = WebEncoders.Base64UrlDecode(partes[2]);
            if (assinatura.Length != 64)
                return false;

            using var chave = AbrirChave();
            if (!chave.VerifyData(
                Encoding.UTF8.GetBytes($"{partes[0]}.{partes[1]}"),
                assinatura, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                return false;

            var conteudo = JsonSerializer.Deserialize<CarteirinhaPayload>(
                WebEncoders.Base64UrlDecode(partes[1]));
            if (conteudo is null || conteudo.Versao < 1 ||
                !Guid.TryParseExact(conteudo.IdentificadorPublico, "N", out _) ||
                conteudo.ValidaAteUnix <= conteudo.EmitidaEmUnix)
                return false;

            payload = conteudo;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or
                                   ArgumentException or OverflowException or
                                   CryptographicException)
        {
            return false;
        }
    }

    private ECDsa AbrirChave()
    {
        var chave = ECDsa.Create();
        try
        {
            chave.ImportPkcs8PrivateKey(_chavePrivadaPkcs8, out var bytesLidos);
            if (bytesLidos != _chavePrivadaPkcs8.Length)
                throw new CryptographicException("Chave privada com bytes adicionais.");
            return chave;
        }
        catch
        {
            chave.Dispose();
            throw;
        }
    }
}
