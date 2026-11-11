using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClickClub.Web.Models;

namespace ClickClub.Web.Services;

// Fachada de consumo HTTP apenas para os módulos de atividades e eventos.
// Toda autorização e regra de negócio continuam exclusivamente na API.
public sealed class ModuloApiWeb(ClickClubApiClient api)
{
    public async Task<(HttpStatusCode Codigo, T? Dados, string? Erro)> ObterAsync<T>(
        string rota, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(HttpMethod.Get, rota, null, ct);
            if (!resposta.IsSuccessStatusCode)
                return (resposta.StatusCode, default, await ErroAsync(resposta, ct));
            var dados = await resposta.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            return dados is null
                ? (HttpStatusCode.BadGateway, default, "Resposta vazia da API.")
                : (resposta.StatusCode, dados, null);
        }
        catch (HttpRequestException) { return (HttpStatusCode.ServiceUnavailable, default, "API indisponível."); }
        catch (TaskCanceledException) { return (HttpStatusCode.GatewayTimeout, default, "Tempo limite da API excedido."); }
        catch (JsonException) { return (HttpStatusCode.BadGateway, default, "Resposta inválida da API."); }
    }

    public async Task<(HttpStatusCode Codigo, string? Erro)> AlterarAsync(
        HttpMethod metodo, string rota, object? dados, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(metodo, rota, dados, ct);
            return resposta.IsSuccessStatusCode
                ? (resposta.StatusCode, null)
                : (resposta.StatusCode, await ErroAsync(resposta, ct));
        }
        catch (HttpRequestException) { return (HttpStatusCode.ServiceUnavailable, "API indisponível."); }
        catch (TaskCanceledException) { return (HttpStatusCode.GatewayTimeout, "Tempo limite da API excedido."); }
        catch (JsonException) { return (HttpStatusCode.BadGateway, "Resposta inválida da API."); }
    }

    private static async Task<string> ErroAsync(
        HttpResponseMessage resposta, CancellationToken ct)
    {
        try
        {
            var erro = await resposta.Content.ReadFromJsonAsync<ErroApi>(
                cancellationToken: ct);
            if (!string.IsNullOrWhiteSpace(erro?.Erro)) return erro.Erro;
        }
        catch (JsonException) { }
        catch (HttpRequestException) { }
        return resposta.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Sessão expirada. Entre novamente.",
            HttpStatusCode.Forbidden => "Operação fora do seu perfil ou clube.",
            HttpStatusCode.NotFound => "Registro não encontrado.",
            _ => "A API não concluiu a operação solicitada."
        };
    }
}
