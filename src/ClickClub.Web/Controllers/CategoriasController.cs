using System.Net;
using System.Net.Http.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "AdministradorGeral")]
public sealed class CategoriasController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/categorias", null, ct);

            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!resposta.IsSuccessStatusCode)
            {
                ViewBag.Erro = "Não foi possível consultar as categorias.";
                return View(new List<CategoriaApi>());
            }

            var itens = await resposta.Content
                .ReadFromJsonAsync<List<CategoriaApi>>(cancellationToken: ct);
            return View(itens ?? []);
        }
        catch (HttpRequestException)
        {
            ViewBag.Erro = "API indisponível.";
            return View(new List<CategoriaApi>());
        }
        catch (TaskCanceledException)
        {
            ViewBag.Erro = "Tempo limite da API excedido.";
            return View(new List<CategoriaApi>());
        }
    }

    [HttpGet]
    public IActionResult Criar() => View("Formulario", new CategoriaFormulario());

    [HttpGet]
    public async Task<IActionResult> Editar(long id, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/categorias/{id}", null, ct);
            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return NotFound();
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (!resposta.IsSuccessStatusCode)
                return StatusCode((int)resposta.StatusCode);

            var categoria = await resposta.Content
                .ReadFromJsonAsync<CategoriaApi>(cancellationToken: ct);
            return categoria is null
                ? Problem("Resposta inválida da API.")
                : View("Formulario", new CategoriaFormulario
                    { Id = categoria.Id, Nome = categoria.Nome });
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
        CategoriaFormulario modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View("Formulario", modelo);

        var rota = modelo.Id == 0
            ? "api/categorias" : $"api/categorias/{modelo.Id}";
        var verbo = modelo.Id == 0 ? HttpMethod.Post : HttpMethod.Put;

        try
        {
            using var resposta = await api.EnviarAsync(verbo, rota, modelo, ct);
            if (resposta.IsSuccessStatusCode)
            {
                TempData["Sucesso"] = "Categoria salva com sucesso.";
                return RedirectToAction(nameof(Index));
            }
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();

            ModelState.AddModelError(string.Empty,
                resposta.StatusCode == HttpStatusCode.Conflict
                    ? "Já existe uma categoria com esse nome."
                    : "Não foi possível salvar a categoria.");
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
                HttpMethod.Patch, $"api/categorias/{id}/situacao",
                new { ativa }, ct);
            TempData[resposta.IsSuccessStatusCode ? "Sucesso" : "Erro"] =
                resposta.IsSuccessStatusCode
                    ? "Situação da categoria atualizada."
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
