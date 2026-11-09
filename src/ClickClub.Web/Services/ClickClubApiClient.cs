using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;

namespace ClickClub.Web.Services;

public sealed class ClickClubApiClient(
    IHttpClientFactory clientFactory,
    IHttpContextAccessor contextAccessor)
{
    public async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod verbo, string rota, object? dados,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient("ClickClubApi");
        using var requisicao = new HttpRequestMessage(verbo, rota);

        var token = contextAccessor.HttpContext is null
            ? null
            : await contextAccessor.HttpContext.GetTokenAsync("access_token");

        if (!string.IsNullOrWhiteSpace(token))
        {
            requisicao.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        if (dados is not null)
            requisicao.Content = JsonContent.Create(dados);

        return await client.SendAsync(requisicao, cancellationToken);
    }
}
