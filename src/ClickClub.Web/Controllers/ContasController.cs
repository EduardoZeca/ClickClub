using System.Net;
using System.Net.Http.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "AdministradorGeral")]
public sealed class ContasController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        long pessoaId, long associacaoId, string papel, CancellationToken ct)
    {
        if (papel is not ("Titular" or "Dependente") || associacaoId <= 0)
            return BadRequest();

        try
        {
            if (papel == "Titular" && pessoaId <= 0)
            {
                using var titularResposta = await api.EnviarAsync(
                    HttpMethod.Get, $"api/titulares/{associacaoId}", null, ct);
                if (!titularResposta.IsSuccessStatusCode)
                    return StatusCode((int)titularResposta.StatusCode);

                var titular = await titularResposta.Content
                    .ReadFromJsonAsync<TitularDetalheApi>(cancellationToken: ct);
                if (titular is null) return Problem("Resposta inválida da API.");
                pessoaId = titular.PessoaId;
            }

            if (pessoaId <= 0) return BadRequest();

            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/contas/pessoa/{pessoaId}", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();

            ViewBag.AssociacaoId = associacaoId;
            ViewBag.Papel = papel;

            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return View("Criar", new ContaCriacaoFormulario
                {
                    PessoaId = pessoaId,
                    AssociacaoId = associacaoId,
                    Papel = papel
                });

            if (!resposta.IsSuccessStatusCode)
                return StatusCode((int)resposta.StatusCode);

            var conta = await resposta.Content
                .ReadFromJsonAsync<ContaAssociativaApi>(cancellationToken: ct);
            return conta is null ? Problem("Resposta inválida da API.")
                : View("Detalhes", conta);
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
    public async Task<IActionResult> Criar(
        ContaCriacaoFormulario modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View("Criar", modelo);

        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Post, "api/contas", modelo.ParaApi(), ct);
            if (resposta.IsSuccessStatusCode)
            {
                TempData["Sucesso"] = "Conta associativa criada com sucesso.";
                return RedirectToAction(nameof(Index), new
                {
                    pessoaId = modelo.PessoaId,
                    associacaoId = modelo.AssociacaoId,
                    papel = modelo.Papel
                });
            }
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();

            var mensagem = "Não foi possível criar a conta.";
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

        modelo.Senha = string.Empty;
        modelo.ConfirmarSenha = string.Empty;
        return View("Criar", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarSituacao(
        long usuarioId, long pessoaId, long associacaoId,
        string papel, bool ativo, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Patch, $"api/contas/{usuarioId}/situacao",
                new { ativo }, ct);
            TempData[resposta.IsSuccessStatusCode ? "Sucesso" : "Erro"] =
                resposta.IsSuccessStatusCode ? "Situação da conta atualizada."
                    : "Não foi possível alterar a situação da conta.";
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível.";
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo limite da API excedido.";
        }

        return RedirectToAction(nameof(Index),
            new { pessoaId, associacaoId, papel });
    }

    [HttpGet]
    public IActionResult Senha(
        long usuarioId, long pessoaId, long associacaoId, string papel)
    {
        if (usuarioId <= 0 || pessoaId <= 0 || associacaoId <= 0 ||
            papel is not ("Titular" or "Dependente"))
            return BadRequest();

        return View(new ContaSenhaFormulario
        {
            UsuarioId = usuarioId,
            PessoaId = pessoaId,
            AssociacaoId = associacaoId,
            Papel = papel
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarSenha(
        ContaSenhaFormulario modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Senha", modelo);

        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Put, $"api/contas/{modelo.UsuarioId}/senha",
                new { novaSenha = modelo.NovaSenha }, ct);
            if (resposta.IsSuccessStatusCode)
            {
                TempData["Sucesso"] = "Senha redefinida com sucesso.";
                return RedirectToAction(nameof(Index), new
                {
                    pessoaId = modelo.PessoaId,
                    associacaoId = modelo.AssociacaoId,
                    papel = modelo.Papel
                });
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
}
