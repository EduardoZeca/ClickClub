using System.Net;
using System.Net.Http.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "AdministradorGeral")]
public sealed class TitularesController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/titulares", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!resposta.IsSuccessStatusCode)
            {
                ViewBag.Erro = "Não foi possível consultar os titulares.";
                return View(new List<TitularResumoApi>());
            }

            var titulares = await resposta.Content
                .ReadFromJsonAsync<List<TitularResumoApi>>(cancellationToken: ct);
            return View(titulares ?? []);
        }
        catch (HttpRequestException)
        {
            ViewBag.Erro = "API indisponível.";
            return View(new List<TitularResumoApi>());
        }
        catch (TaskCanceledException)
        {
            ViewBag.Erro = "Tempo limite de comunicação com a API.";
            return View(new List<TitularResumoApi>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var modelo = new TitularFormulario();
        return await PrepararFormularioAsync(modelo, ct)
            ? View("Formulario", modelo)
            : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(long id, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/titulares/{id}", null, ct);
            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return NotFound();
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (!resposta.IsSuccessStatusCode)
                return StatusCode((int)resposta.StatusCode);

            var titular = await resposta.Content
                .ReadFromJsonAsync<TitularDetalheApi>(cancellationToken: ct);
            if (titular is null) return Problem("Resposta inválida da API.");

            var modelo = TitularFormulario.De(titular);
            return await PrepararFormularioAsync(modelo, ct)
                ? View("Formulario", modelo)
                : RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> Salvar(TitularFormulario modelo,
        CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var rota = modelo.Id == 0
                ? "api/titulares" : $"api/titulares/{modelo.Id}";
            var verbo = modelo.Id == 0 ? HttpMethod.Post : HttpMethod.Put;

            try
            {
                using var resposta = await api.EnviarAsync(
                    verbo, rota, modelo.ParaApi(), ct);
                if (resposta.IsSuccessStatusCode)
                {
                    TempData["Sucesso"] = "Cadastro associativo salvo com sucesso.";
                    return RedirectToAction(nameof(Index));
                }
                if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToAction("Entrar", "Conta");
                if (resposta.StatusCode == HttpStatusCode.Forbidden)
                    return Forbid();

                var mensagem = "Não foi possível salvar o titular.";
                if (resposta.StatusCode is HttpStatusCode.BadRequest or
                    HttpStatusCode.Conflict)
                {
                    var erro = await resposta.Content
                        .ReadFromJsonAsync<ErroApi>(cancellationToken: ct);
                    if (!string.IsNullOrWhiteSpace(erro?.Erro))
                        mensagem = erro.Erro;
                }

                ModelState.AddModelError(string.Empty, mensagem);
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "API indisponível.");
            }
            catch (TaskCanceledException)
            {
                ModelState.AddModelError(string.Empty, "Tempo limite da API excedido.");
            }
        }

        if (!await PrepararFormularioAsync(modelo, ct))
            ModelState.AddModelError(string.Empty,
                "Não foi possível carregar as opções cadastrais. Tente novamente.");
        return View("Formulario", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarSituacao(
        long id, bool ativar, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Patch, $"api/titulares/{id}/situacao",
                new { status = ativar ? "Ativa" : "Suspensa" }, ct);
            TempData[resposta.IsSuccessStatusCode ? "Sucesso" : "Erro"] =
                resposta.IsSuccessStatusCode
                    ? "Situação da associação atualizada."
                    : "Não foi possível atualizar a situação da associação.";
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

    private async Task<bool> PrepararFormularioAsync(
        TitularFormulario modelo, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/titulares/opcoes", null, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                TempData["Erro"] = "Não foi possível consultar as opções cadastrais.";
                return false;
            }

            modelo.Opcoes = await resposta.Content
                .ReadFromJsonAsync<TitularOpcoesApi>(cancellationToken: ct);
            return modelo.Opcoes is not null;
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível.";
            return false;
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo limite da API excedido.";
            return false;
        }
    }
}
