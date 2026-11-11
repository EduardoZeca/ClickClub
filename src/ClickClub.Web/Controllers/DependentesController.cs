using System.Net;
using System.Net.Http.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "AdministradorGeral")]
public sealed class DependentesController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(long associacaoId, CancellationToken ct)
    {
        if (associacaoId <= 0) return BadRequest();
        ViewBag.AssociacaoId = associacaoId;
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/dependentes?associacaoId={associacaoId}",
                null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!resposta.IsSuccessStatusCode)
            {
                ViewBag.Erro = "Não foi possível consultar dependentes.";
                return View(new List<DependenteResumoApi>());
            }

            var itens = await resposta.Content
                .ReadFromJsonAsync<List<DependenteResumoApi>>(cancellationToken: ct);
            return View(itens ?? []);
        }
        catch (HttpRequestException)
        {
            ViewBag.Erro = "API indisponível.";
            return View(new List<DependenteResumoApi>());
        }
        catch (TaskCanceledException)
        {
            ViewBag.Erro = "Tempo limite da API excedido.";
            return View(new List<DependenteResumoApi>());
        }
    }

    [HttpGet]
    public IActionResult Criar(long associacaoId)
    {
        if (associacaoId <= 0) return BadRequest();
        return View("Formulario", new DependenteFormulario
        {
            AssociacaoId = associacaoId
        });
    }

    [HttpGet]
    public async Task<IActionResult> Editar(long id, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/dependentes/{id}", null, ct);
            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return NotFound();
            if (!resposta.IsSuccessStatusCode)
                return StatusCode((int)resposta.StatusCode);
            var item = await resposta.Content
                .ReadFromJsonAsync<DependenteDetalheApi>(cancellationToken: ct);
            return item is null ? Problem("Resposta inválida da API.")
                : View("Formulario", DependenteFormulario.De(item));
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salvar(
        DependenteFormulario modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View("Formulario", modelo);

        var rota = modelo.Id == 0
            ? "api/dependentes" : $"api/dependentes/{modelo.Id}";
        var verbo = modelo.Id == 0 ? HttpMethod.Post : HttpMethod.Put;

        try
        {
            using var resposta = await api.EnviarAsync(
                verbo, rota, modelo.ParaApi(), ct);
            if (resposta.IsSuccessStatusCode)
            {
                TempData["Sucesso"] = "Dependente salvo com sucesso.";
                return RedirectToAction(nameof(Index),
                    new { associacaoId = modelo.AssociacaoId });
            }
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();

            var mensagem = "Não foi possível salvar o dependente.";
            if (resposta.StatusCode is HttpStatusCode.BadRequest or
                HttpStatusCode.Conflict)
            {
                var erro = await resposta.Content
                    .ReadFromJsonAsync<ErroApi>(cancellationToken: ct);
                if (!string.IsNullOrWhiteSpace(erro?.Erro)) mensagem = erro.Erro;
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

        return View("Formulario", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarSituacao(
        long id, long associacaoId, bool ativo, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Patch, $"api/dependentes/{id}/situacao",
                new { ativo }, ct);
            if (resposta.IsSuccessStatusCode)
                TempData["Sucesso"] = "Situação do dependente atualizada.";
            else
            {
                var mensagem = "Não foi possível alterar a situação do dependente.";
                if (resposta.StatusCode is HttpStatusCode.BadRequest or
                    HttpStatusCode.Conflict)
                {
                    var erro = await resposta.Content.ReadFromJsonAsync<ErroApi>(
                        cancellationToken: ct);
                    if (!string.IsNullOrWhiteSpace(erro?.Erro)) mensagem = erro.Erro;
                }
                TempData["Erro"] = mensagem;
            }
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível.";
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo limite da API excedido.";
        }

        return RedirectToAction(nameof(Index), new { associacaoId });
    }
}
