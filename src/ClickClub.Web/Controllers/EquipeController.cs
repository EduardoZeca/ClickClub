using System.Net;
using System.Net.Http.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "AdministradorGeral")]
public sealed class EquipeController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/equipe", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (!resposta.IsSuccessStatusCode)
            {
                ViewBag.Erro = "Não foi possível consultar a equipe.";
                return View(new List<EquipeUsuarioApi>());
            }

            var itens = await resposta.Content
                .ReadFromJsonAsync<List<EquipeUsuarioApi>>(cancellationToken: ct);
            return View(itens ?? []);
        }
        catch (HttpRequestException)
        {
            ViewBag.Erro = "API indisponível.";
            return View(new List<EquipeUsuarioApi>());
        }
        catch (TaskCanceledException)
        {
            ViewBag.Erro = "Tempo limite da API excedido.";
            return View(new List<EquipeUsuarioApi>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var modelo = new EquipeCriacaoFormulario();
        return await PrepararUnidadesAsync(modelo, ct)
            ? View(modelo) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Criar(
        EquipeCriacaoFormulario modelo, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                using var resposta = await api.EnviarAsync(
                    HttpMethod.Post, "api/equipe", modelo.ParaApi(), ct);
                if (resposta.IsSuccessStatusCode)
                {
                    TempData["Sucesso"] = "Conta funcional criada com sucesso.";
                    return RedirectToAction(nameof(Index));
                }
                if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToAction("Entrar", "Conta");

                var mensagem = "Não foi possível criar a conta funcional.";
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
        }

        await PrepararUnidadesAsync(modelo, ct);
        modelo.Senha = string.Empty;
        modelo.ConfirmarSenha = string.Empty;
        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(long id, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/equipe/{id}", null, ct);
            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return NotFound();
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (!resposta.IsSuccessStatusCode)
                return StatusCode((int)resposta.StatusCode);

            var usuario = await resposta.Content
                .ReadFromJsonAsync<EquipeUsuarioApi>(cancellationToken: ct);
            if (usuario is null) return Problem("Resposta inválida da API.");

            var unidades = await ConsultarUnidadesAsync(ct);
            if (unidades is null) return Problem("Não foi possível consultar os clubes.");
            return View(new EquipeDetalhesPagina(usuario, unidades));
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
    public async Task<IActionResult> Atribuir(
        EquipeAtribuicaoFormulario modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Erro"] = "Selecione um papel e um clube válidos.";
            return RedirectToAction(nameof(Detalhes), new { id = modelo.UsuarioId });
        }

        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Post, $"api/equipe/{modelo.UsuarioId}/atribuicoes",
                new { modelo.Papel, modelo.UnidadeId }, ct);
            TempData[resposta.IsSuccessStatusCode ? "Sucesso" : "Erro"] =
                resposta.IsSuccessStatusCode
                    ? "Atribuição adicionada."
                    : "Atribuição rejeitada (verifique duplicidade e clube ativo).";
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível.";
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo limite da API excedido.";
        }

        return RedirectToAction(nameof(Detalhes), new { id = modelo.UsuarioId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revogar(
        EquipeAtribuicaoFormulario modelo, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Delete, $"api/equipe/{modelo.UsuarioId}/atribuicoes",
                new { modelo.Papel, modelo.UnidadeId }, ct);
            TempData[resposta.IsSuccessStatusCode ? "Sucesso" : "Erro"] =
                resposta.IsSuccessStatusCode
                    ? "Atribuição revogada."
                    : "Não foi possível revogar a atribuição.";
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível.";
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo limite da API excedido.";
        }

        return RedirectToAction(nameof(Detalhes), new { id = modelo.UsuarioId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarSituacao(
        long id, bool ativo, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Patch, $"api/equipe/{id}/situacao",
                new { ativo }, ct);
            TempData[resposta.IsSuccessStatusCode ? "Sucesso" : "Erro"] =
                resposta.IsSuccessStatusCode
                    ? "Situação da conta atualizada."
                    : "Não foi possível atualizar a situação.";
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível.";
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo limite da API excedido.";
        }
        return RedirectToAction(nameof(Detalhes), new { id });
    }

    [HttpGet]
    public IActionResult Senha(long id)
    {
        if (id <= 0) return BadRequest();
        return View(new EquipeSenhaFormulario { UsuarioId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarSenha(
        EquipeSenhaFormulario modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Senha", modelo);

        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Put, $"api/equipe/{modelo.UsuarioId}/senha",
                new { novaSenha = modelo.NovaSenha }, ct);
            if (resposta.IsSuccessStatusCode)
            {
                TempData["Sucesso"] = "Senha funcional redefinida.";
                return RedirectToAction(nameof(Detalhes), new { id = modelo.UsuarioId });
            }
            ModelState.AddModelError(string.Empty,
                "Não foi possível redefinir a senha desta conta.");
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "API indisponível.");
        }
        catch (TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty, "Tempo limite da API excedido.");
        }

        modelo.NovaSenha = string.Empty;
        modelo.ConfirmarNovaSenha = string.Empty;
        return View("Senha", modelo);
    }

    private async Task<bool> PrepararUnidadesAsync(
        EquipeCriacaoFormulario modelo, CancellationToken ct)
    {
        try
        {
            modelo.Unidades = await ConsultarUnidadesAsync(ct) ?? [];
            if (modelo.Unidades.Count > 0) return true;
            TempData["Erro"] = "Cadastre pelo menos um clube antes da equipe.";
            return false;
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

    private async Task<List<UnidadeApi>?> ConsultarUnidadesAsync(CancellationToken ct)
    {
        using var resposta = await api.EnviarAsync(
            HttpMethod.Get, "api/unidades", null, ct);
        if (!resposta.IsSuccessStatusCode) return null;
        return await resposta.Content
            .ReadFromJsonAsync<List<UnidadeApi>>(cancellationToken: ct);
    }
}
