using System.Net;
using System.Net.Http.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles =
    "AdministradorGeral,FuncionarioUnidade,Porteiro,PresidenteClube,Tesoureiro")]
public sealed class MeusClubesController(ClickClubApiClient api)
    : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/meus-clubes", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!resposta.IsSuccessStatusCode)
                return Problem("Não foi possível consultar as unidades atribuídas.");

            var clubes = await resposta.Content
                .ReadFromJsonAsync<List<ClubeAtribuidoApi>>(cancellationToken: ct);
            return View(clubes ?? []);
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

    [HttpGet]
    public async Task<IActionResult> Detalhes(long id, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/meus-clubes/{id}", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!resposta.IsSuccessStatusCode)
                return StatusCode((int)resposta.StatusCode);

            var clube = await resposta.Content
                .ReadFromJsonAsync<ClubeAtribuidoApi>(cancellationToken: ct);
            return clube is null ? Problem("Resposta inválida da API.")
                : View(clube);
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
