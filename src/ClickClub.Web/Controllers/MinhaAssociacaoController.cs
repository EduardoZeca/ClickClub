using System.Net;
using System.Net.Http.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "Titular,Dependente")]
public sealed class MinhaAssociacaoController(ClickClubApiClient api)
    : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/minha-associacao", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return NotFound("Vínculo associativo não encontrado.");
            if (!resposta.IsSuccessStatusCode)
                return Problem("Não foi possível consultar a associação.");

            var modelo = await resposta.Content
                .ReadFromJsonAsync<MinhaAssociacaoApi>(cancellationToken: ct);
            return modelo is null ? Problem("Resposta inválida da API.")
                : View(modelo);
        }
        catch (HttpRequestException)
        {
            return Problem("API indisponível.");
        }
        catch (TaskCanceledException)
        {
            return Problem("Tempo limite da API excedido.");
        }
    }
}
