using System.Text.Json;
using ClickClub.Mobile.Modelos;
using Microsoft.Maui.Storage;

namespace ClickClub.Mobile.Servicos;

// Credencial armazenada de forma protegida pelo Android; não guarda chave privada.
// O QR offline não substitui a validação da API pela portaria.
public sealed class CarteirinhaLocal
{
    private const string Chave = "clickclub.carteirinha.atual";
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    public async Task<CarteirinhaApi?> LerAsync()
    {
        try
        {
            var texto = await SecureStorage.Default.GetAsync(Chave);
            return string.IsNullOrEmpty(texto)
                ? null : JsonSerializer.Deserialize<CarteirinhaApi>(texto, Opcoes);
        }
        catch (Exception) { return null; }
    }

    public Task GuardarAsync(CarteirinhaApi carteira)
    {
        if (!carteira.EstaValida)
            throw new InvalidOperationException("Não é permitido salvar QR Code vencido.");
        return SecureStorage.Default.SetAsync(Chave, JsonSerializer.Serialize(carteira, Opcoes));
    }

    public void Limpar()
    {
        try { SecureStorage.Default.Remove(Chave); }
        catch (Exception) { }
    }
}
