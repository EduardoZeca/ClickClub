using System.Net;
using System.Net.Http.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "AdministradorGeral,FuncionarioUnidade,PresidenteClube")]
public sealed class EspacosController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/espacos", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!resposta.IsSuccessStatusCode)
                return Problem("Não foi possível consultar os espaços.");
            return View(await resposta.Content.ReadFromJsonAsync<List<EspacoApi>>(
                cancellationToken: ct) ?? []);
        }
        catch (HttpRequestException)
        {
            return Problem("API indisponível.");
        }
        catch (TaskCanceledException)
        {
            return Problem("Tempo limite de comunicação com a API.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var modelo = new EspacoFormulario();
        await CarregarClubesAsync(modelo, ct);
        return View("Formulario", modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Editar(long id, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/espacos/{id}", null, ct);
            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return NotFound();
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (!resposta.IsSuccessStatusCode)
                return StatusCode((int)resposta.StatusCode);
            var espaco = await resposta.Content.ReadFromJsonAsync<EspacoApi>(
                cancellationToken: ct);
            if (espaco is null) return Problem("Resposta inválida da API.");
            var modelo = EspacoFormulario.De(espaco);
            await CarregarClubesAsync(modelo, ct);
            return View("Formulario", modelo);
        }
        catch (HttpRequestException)
        {
            return Problem("API indisponível.");
        }
        catch (TaskCanceledException)
        {
            return Problem("Tempo limite de comunicação com a API.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salvar(
        EspacoFormulario modelo, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var metodo = modelo.Id == 0 ? HttpMethod.Post : HttpMethod.Put;
                var rota = modelo.Id == 0
                    ? "api/espacos" : $"api/espacos/{modelo.Id}";
                using var resposta = await api.EnviarAsync(metodo, rota, new
                {
                    modelo.UnidadeId, modelo.Nome, modelo.Tipo,
                    modelo.Capacidade, modelo.TaxaBase
                }, ct);
                if (resposta.IsSuccessStatusCode)
                {
                    TempData["Sucesso"] = "Espaço salvo com sucesso.";
                    return RedirectToAction(nameof(Index));
                }
                if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToAction("Entrar", "Conta");
                if (resposta.StatusCode == HttpStatusCode.Forbidden)
                    return Forbid();

                if (resposta.StatusCode is HttpStatusCode.Conflict or
                    HttpStatusCode.BadRequest)
                {
                    var erro = await resposta.Content
                        .ReadFromJsonAsync<ErroApi>(cancellationToken: ct);
                    ModelState.AddModelError(string.Empty,
                        erro?.Erro ?? "Verifique o nome, o clube e os demais dados.");
                }
                else ModelState.AddModelError(string.Empty,
                    "Não foi possível salvar o espaço.");
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "API indisponível.");
            }
            catch (TaskCanceledException)
            {
                ModelState.AddModelError(string.Empty,
                    "Tempo limite de comunicação com a API.");
            }
        }

        await CarregarClubesAsync(modelo, ct);
        return View("Formulario", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarSituacao(
        long id, bool ativo, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(HttpMethod.Patch,
                $"api/espacos/{id}/situacao", new { ativo }, ct);
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            TempData[resposta.IsSuccessStatusCode ? "Sucesso" : "Erro"] =
                resposta.IsSuccessStatusCode
                    ? "Situação do espaço atualizada."
                    : "A alteração da situação foi rejeitada pela API.";
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível.";
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo limite de comunicação com a API.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task CarregarClubesAsync(
        EspacoFormulario modelo, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/espacos/clubes", null, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty,
                    "Não foi possível carregar os clubes autorizados.");
                return;
            }
            modelo.Clubes = await resposta.Content
                .ReadFromJsonAsync<List<ClubeEspacoOpcaoApi>>(
                    cancellationToken: ct) ?? [];
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "API indisponível.");
        }
        catch (TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty,
                "Tempo limite de comunicação com a API.");
        }
    }
}
