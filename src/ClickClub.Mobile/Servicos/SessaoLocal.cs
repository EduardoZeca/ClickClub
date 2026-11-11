using Microsoft.Maui.Storage;

namespace ClickClub.Mobile.Servicos;

public sealed class SessaoLocal
{
    private const string ChaveToken = "clickclub.usuario.jwt";

    public async Task<string?> LerTokenAsync()
    {
        try { return await SecureStorage.Default.GetAsync(ChaveToken); }
        catch (Exception) { return null; }
    }

    public Task GuardarTokenAsync(string token) =>
        SecureStorage.Default.SetAsync(ChaveToken, token);

    public void LimparToken()
    {
        try { SecureStorage.Default.Remove(ChaveToken); }
        catch (Exception) { /* Se não houver armazenamento disponível, não há sessão reutilizável. */ }
    }
}
