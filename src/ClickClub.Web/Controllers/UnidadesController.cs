using System.Net;
using System.Net.Http.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "AdministradorGeral")]
public sealed class UnidadesController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/unidades", null, ct);

            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();

            if (!resposta.IsSuccessStatusCode)
            {
                ViewBag.Erro = "Não foi possível consultar os clubes.";
                return View(new List<UnidadeApi>());
            }

            var itens = await resposta.Content
                .ReadFromJsonAsync<List<UnidadeApi>>(cancellationToken: ct);
            return View(itens ?? []);
        }
        catch (HttpRequestException)
        {
            ViewBag.Erro = "API indisponível.";
            return View(new List<UnidadeApi>());
        }
        catch (TaskCanceledException)
        {
            ViewBag.Erro = "Tempo limite de comunicação com a API.";
            return View(new List<UnidadeApi>());
        }
    }

    [HttpGet]
    public IActionResult Criar() => View("Formulario", new UnidadeFormulario());

    [HttpGet]
    public async Task<IActionResult> Editar(long id, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/unidades/{id}", null, ct);

            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return NotFound();
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (!resposta.IsSuccessStatusCode)
                return StatusCode((int)resposta.StatusCode);

            var unidade = await resposta.Content
                .ReadFromJsonAsync<UnidadeApi>(cancellationToken: ct);
            return unidade is null
                ? Problem("Resposta inválida da API.")
                : View("Formulario", UnidadeFormulario.De(unidade));
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível.";
            return RedirectToAction(nameof(Index));
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo limite da API excedido.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salvar(
        UnidadeFormulario modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View("Formulario", modelo);

        var rota = modelo.Id == 0
            ? "api/unidades"
            : $"api/unidades/{modelo.Id}";
        var verbo = modelo.Id == 0 ? HttpMethod.Post : HttpMethod.Put;

        try
        {
            using var resposta = await api.EnviarAsync(verbo, rota, modelo, ct);
            if (resposta.IsSuccessStatusCode)
            {
                TempData["Sucesso"] = "Clube salvo com sucesso.";
                return RedirectToAction(nameof(Index));
            }

            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();

            ModelState.AddModelError(string.Empty,
                resposta.StatusCode == HttpStatusCode.Conflict
                    ? "Já existe um clube com essa sigla."
                    : "Não foi possível salvar o clube. Confira os dados.");
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "API indisponível.");
        }
        catch (TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty, "Tempo limite da API excedido.");
        }

        return View("Formulario", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarSituacao(
        long id, bool ativa, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Patch, $"api/unidades/{id}/situacao",
                new { ativa }, ct);
            TempData[resposta.IsSuccessStatusCode ? "Sucesso" : "Erro"] =
                resposta.IsSuccessStatusCode
                    ? "Situação do clube atualizada."
                    : "Não foi possível alterar a situação.";
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível.";
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo limite da API excedido.";
        }

        return RedirectToAction(nameof(Index));
    }
}
