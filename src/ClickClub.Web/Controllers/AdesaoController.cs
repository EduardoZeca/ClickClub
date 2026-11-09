using System.Net;
using System.Net.Http.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[AllowAnonymous]
public sealed class AdesaoController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Solicitar(CancellationToken ct)
    {
        var modelo = new SolicitacaoAdesaoFormulario();
        await CarregarOpcoesAsync(modelo, ct);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Solicitar(
        SolicitacaoAdesaoFormulario modelo, CancellationToken ct)
    {
        var interesses = (modelo.InteressesTexto ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (interesses.Length > 20 || interesses.Any(x => x.Length > 100))
            ModelState.AddModelError(nameof(modelo.InteressesTexto),
                "Informe até 20 interesses, com no máximo 100 caracteres cada.");

        if (ModelState.IsValid)
        {
            try
            {
                using var resposta = await api.EnviarAsync(
                    HttpMethod.Post, "api/adesoes/solicitar", modelo.ParaApi(), ct);
                if (resposta.StatusCode == HttpStatusCode.Created)
                {
                    var criada = await resposta.Content
                        .ReadFromJsonAsync<AdesaoCriadaApi>(cancellationToken: ct);
                    if (criada is not null)
                    {
                        Response.Headers.CacheControl = "no-store";
                        return View("Comprovante", criada);
                    }
                }
                await AdicionarErroApiAsync(resposta, ct);
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "API indisponível.");
            }
            catch (TaskCanceledException)
            {
                ModelState.AddModelError(string.Empty, "Tempo de resposta da API excedido.");
            }
        }

        await CarregarOpcoesAsync(modelo, ct);
        return View(modelo);
    }

    [HttpGet]
    public IActionResult Consultar() => View(new ConsultaAdesaoFormulario());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Consultar(
        ConsultaAdesaoFormulario modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(modelo);
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Post, "api/adesoes/consultar", new { modelo.Codigo }, ct);
            if (resposta.IsSuccessStatusCode)
            {
                modelo.Resultado = await resposta.Content
                    .ReadFromJsonAsync<AdesaoConsultaApi>(cancellationToken: ct);
            }
            else
                await AdicionarErroApiAsync(resposta, ct);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "API indisponível.");
        }
        catch (TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty, "Tempo de resposta da API excedido.");
        }

        Response.Headers.CacheControl = "no-store";
        return View(modelo);
    }

    private async Task CarregarOpcoesAsync(
        SolicitacaoAdesaoFormulario modelo, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/adesoes/opcoes", null, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty,
                    "Não foi possível consultar clubes e vínculos disponíveis.");
                return;
            }
            var opcoes = await resposta.Content
                .ReadFromJsonAsync<OpcoesAdesaoApi>(cancellationToken: ct);
            if (opcoes is null) return;
            modelo.Clubes = opcoes.Clubes;
            modelo.TiposVinculo = opcoes.TiposVinculo;
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "API indisponível.");
        }
        catch (TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty, "Tempo de resposta da API excedido.");
        }
    }

    private async Task AdicionarErroApiAsync(
        HttpResponseMessage resposta, CancellationToken ct)
    {
        if (resposta.StatusCode == HttpStatusCode.TooManyRequests)
        {
            ModelState.AddModelError(string.Empty,
                "Muitas tentativas. Aguarde um minuto antes de tentar novamente.");
            return;
        }

        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            ModelState.AddModelError(string.Empty, "Código não encontrado.");
            return;
        }

        if (resposta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            var erro = await resposta.Content
                .ReadFromJsonAsync<ErroApi>(cancellationToken: ct);
            if (!string.IsNullOrWhiteSpace(erro?.Erro))
            {
                ModelState.AddModelError(string.Empty, erro.Erro);
                return;
            }
        }
        ModelState.AddModelError(string.Empty,
            "Não foi possível completar a operação. Verifique os dados e tente novamente.");
    }
}
