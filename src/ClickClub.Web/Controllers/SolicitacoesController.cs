using System.Net;
using System.Net.Http.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "AdministradorGeral,PresidenteClube")]
public sealed class SolicitacoesController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/solicitacoes-adesao", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (!resposta.IsSuccessStatusCode)
                return Problem("Não foi possível listar as solicitações.");

            var lista = await resposta.Content
                .ReadFromJsonAsync<List<AdesaoResumoApi>>(cancellationToken: ct);
            return View(lista ?? []);
        }
        catch (HttpRequestException)
        {
            return Problem("API indisponível.");
        }
        catch (TaskCanceledException)
        {
            return Problem("Tempo de resposta da API excedido.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(long id, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/solicitacoes-adesao/{id}", null, ct);
            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return NotFound();
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (!resposta.IsSuccessStatusCode)
                return StatusCode((int)resposta.StatusCode);
            var detalhe = await resposta.Content
                .ReadFromJsonAsync<AdesaoDetalheApi>(cancellationToken: ct);
            if (detalhe is null) return Problem("Resposta inválida da API.");

            using var categoriasResposta = await api.EnviarAsync(
                HttpMethod.Get, "api/solicitacoes-adesao/categorias", null, ct);
            if (!categoriasResposta.IsSuccessStatusCode)
                return Problem("Não foi possível obter as categorias associativas.");
            var categorias = await categoriasResposta.Content
                .ReadFromJsonAsync<List<CategoriaAdesaoOpcao>>(cancellationToken: ct);
            return View(new AdesaoDetalhesPagina(detalhe, categorias ?? []));
        }
        catch (HttpRequestException)
        {
            return Problem("API indisponível.");
        }
        catch (TaskCanceledException)
        {
            return Problem("Tempo de resposta da API excedido.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decidir(
        DecisaoAdesaoFormulario formulario, CancellationToken ct)
    {
        if (formulario.Aprovar &&
            (formulario.CategoriaId is null or <= 0 ||
             string.IsNullOrWhiteSpace(formulario.Matricula)))
        {
            TempData["Erro"] = "Selecione a categoria e informe a matrícula para aprovar.";
            return RedirectToAction(nameof(Detalhes), new { id = formulario.Id });
        }
        if (!formulario.Aprovar && string.IsNullOrWhiteSpace(formulario.Motivo))
        {
            TempData["Erro"] = "Informe o motivo para rejeitar a solicitação.";
            return RedirectToAction(nameof(Detalhes), new { id = formulario.Id });
        }
        if (!ModelState.IsValid)
        {
            TempData["Erro"] = "Dados inválidos para a decisão.";
            return RedirectToAction(nameof(Detalhes), new { id = formulario.Id });
        }

        try
        {
            using var resposta = await api.EnviarAsync(HttpMethod.Post,
                $"api/solicitacoes-adesao/{formulario.Id}/decisao", new
                {
                    formulario.Aprovar,
                    formulario.CategoriaId,
                    formulario.Matricula,
                    formulario.Motivo
                }, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (resposta.IsSuccessStatusCode)
            {
                TempData["Sucesso"] = formulario.Aprovar
                    ? "Solicitação aprovada e associação ativada."
                    : "Solicitação rejeitada.";
                return RedirectToAction(nameof(Index));
            }
            if (resposta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                var erro = await resposta.Content
                    .ReadFromJsonAsync<ErroApi>(cancellationToken: ct);
                TempData["Erro"] = erro?.Erro ?? "A solicitação não pôde ser analisada.";
            }
            else
                TempData["Erro"] = "A decisão não foi aceita pela API.";
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível.";
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo de resposta da API excedido.";
        }

        return RedirectToAction(nameof(Detalhes), new { id = formulario.Id });
    }
}
