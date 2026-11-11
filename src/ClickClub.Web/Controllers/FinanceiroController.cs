using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "AdministradorGeral,Titular,Tesoureiro")]
public sealed class FinanceiroController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var rota = User.IsInRole("AdministradorGeral") || User.IsInRole("Tesoureiro")
            ? "api/financeiro/faturas" : "api/financeiro/faturas/minhas";
        try
        {
            using var resposta = await api.EnviarAsync(HttpMethod.Get, rota, null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!resposta.IsSuccessStatusCode)
            {
                ViewBag.Erro = "Não foi possível consultar as faturas.";
                return View(new List<FaturaResumoApi>());
            }

            var itens = await resposta.Content.ReadFromJsonAsync<List<FaturaResumoApi>>(
                cancellationToken: ct);
            return View(itens ?? []);
        }
        catch (HttpRequestException)
        {
            ViewBag.Erro = "API indisponível.";
            return View(new List<FaturaResumoApi>());
        }
        catch (TaskCanceledException)
        {
            ViewBag.Erro = "Tempo limite da API excedido.";
            return View(new List<FaturaResumoApi>());
        }
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,Tesoureiro")]
    public async Task<IActionResult> Locacoes(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/financeiro/locacoes", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!resposta.IsSuccessStatusCode)
                return StatusCode((int)resposta.StatusCode);

            var itens = await resposta.Content
                .ReadFromJsonAsync<List<LocacaoFinanceiraApi>>(cancellationToken: ct);
            return View(itens ?? []);
        }
        catch (HttpRequestException)
        {
            ViewBag.Erro = "API indisponível.";
            return View(new List<LocacaoFinanceiraApi>());
        }
        catch (TaskCanceledException)
        {
            ViewBag.Erro = "Tempo limite da API excedido.";
            return View(new List<LocacaoFinanceiraApi>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(long id, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/financeiro/faturas/{id}", null, ct);
            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return NotFound();
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!resposta.IsSuccessStatusCode)
                return StatusCode((int)resposta.StatusCode);
            var detalhe = await resposta.Content.ReadFromJsonAsync<FaturaDetalheApi>(
                cancellationToken: ct);
            return detalhe is null ? Problem("Resposta financeira inválida.") : View(detalhe);
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
    public async Task<IActionResult> Quitar(long id, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(HttpMethod.Post,
                $"api/financeiro/faturas/{id}/quitar", null, ct);
            if (resposta.IsSuccessStatusCode)
            {
                var quitacao = await resposta.Content
                    .ReadFromJsonAsync<QuitacaoMensalidadeApi>(cancellationToken: ct);
                if (quitacao is null) return Problem("Resposta de quitação inválida.");
                TempData["Sucesso"] = quitacao.JaQuitada
                    ? "Fatura já quitada. Nenhum pagamento simulado foi duplicado."
                    : "Quitação simulada de " +
                      quitacao.ValorQuitado.ToString("C", CultureInfo.GetCultureInfo("pt-BR")) +
                      ". Multa aplicada nesta operação: " +
                      quitacao.MultaAplicada.ToString("C", CultureInfo.GetCultureInfo("pt-BR")) + ".";
                return RedirectToAction(nameof(Detalhes), new { id });
            }
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return NotFound();
            TempData["Erro"] = await LerErroAsync(resposta,
                "Não foi possível registrar a quitação simulada.", ct);
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
    [Authorize(Roles = "AdministradorGeral")]
    public async Task<IActionResult> Politicas(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/financeiro/politicas", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!resposta.IsSuccessStatusCode)
            {
                ViewBag.Erro = "Não foi possível consultar as políticas.";
                return View(new List<PoliticaFinanceiraApi>());
            }
            var itens = await resposta.Content.ReadFromJsonAsync<List<PoliticaFinanceiraApi>>(
                cancellationToken: ct);
            return View(itens ?? []);
        }
        catch (HttpRequestException)
        {
            ViewBag.Erro = "API indisponível.";
            return View(new List<PoliticaFinanceiraApi>());
        }
        catch (TaskCanceledException)
        {
            ViewBag.Erro = "Tempo limite da API excedido.";
            return View(new List<PoliticaFinanceiraApi>());
        }
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral")]
    public async Task<IActionResult> CriarPolitica(CancellationToken ct)
    {
        var modelo = new PoliticaFinanceiraFormulario();
        if (!await CarregarOpcoesAsync(modelo, ct))
            return RedirectToAction(nameof(Politicas));
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral")]
    public async Task<IActionResult> SalvarPolitica(
        PoliticaFinanceiraFormulario modelo, CancellationToken ct)
    {
        if (modelo.VigenciaInicio?.Day != 1)
            ModelState.AddModelError(nameof(modelo.VigenciaInicio),
                "Informe o dia 1º do mês de início.");

        if (ModelState.IsValid)
        {
            try
            {
                using var resposta = await api.EnviarAsync(HttpMethod.Post,
                    "api/financeiro/politicas", new
                    {
                        modelo.UnidadeId, modelo.CategoriaId,
                        modelo.ValorMensal, modelo.VigenciaInicio
                    }, ct);
                if (resposta.IsSuccessStatusCode)
                {
                    TempData["Sucesso"] = "Política mensal cadastrada.";
                    return RedirectToAction(nameof(Politicas));
                }
                if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToAction("Entrar", "Conta");
                if (resposta.StatusCode == HttpStatusCode.Forbidden)
                    return Forbid();
                ModelState.AddModelError(string.Empty,
                    await LerErroAsync(resposta, "Não foi possível salvar a política.", ct));
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

        await CarregarOpcoesAsync(modelo, ct);
        return View("CriarPolitica", modelo);
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral")]
    public async Task<IActionResult> Emitir(CancellationToken ct)
    {
        var modelo = new EmissaoMensalidadeFormulario();
        if (!await CarregarOpcoesAsync(modelo, ct))
            return RedirectToAction(nameof(Index));
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral")]
    public async Task<IActionResult> ConfirmarEmissao(
        EmissaoMensalidadeFormulario modelo, CancellationToken ct)
    {
        if (modelo.Competencia?.Day != 1)
            ModelState.AddModelError(nameof(modelo.Competencia),
                "Informe o dia 1º do mês da competência.");

        if (ModelState.IsValid)
        {
            try
            {
                using var resposta = await api.EnviarAsync(HttpMethod.Post,
                    "api/financeiro/faturas/emitir", new
                    {
                        modelo.AssociacaoId, modelo.Competencia
                    }, ct);
                if (resposta.IsSuccessStatusCode)
                {
                    var emissao = await resposta.Content
                        .ReadFromJsonAsync<EmissaoMensalidadeApi>(cancellationToken: ct);
                    if (emissao is null) return Problem("Resposta de emissão inválida.");
                    TempData["Sucesso"] = emissao.JaExistia
                        ? "Esta fatura já existia; nenhuma cobrança foi duplicada."
                        : "Mensalidade emitida de forma simulada.";
                    return RedirectToAction(nameof(Detalhes), new { id = emissao.FaturaId });
                }
                if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToAction("Entrar", "Conta");
                if (resposta.StatusCode == HttpStatusCode.Forbidden)
                    return Forbid();
                ModelState.AddModelError(string.Empty,
                    await LerErroAsync(resposta, "Não foi possível emitir a mensalidade.", ct));
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

        await CarregarOpcoesAsync(modelo, ct);
        return View("Emitir", modelo);
    }

    private async Task<bool> CarregarOpcoesAsync(
        PoliticaFinanceiraFormulario modelo, CancellationToken ct)
    {
        var opcoes = await ConsultarOpcoesAsync(ct);
        if (opcoes is null)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível carregar clubes/categorias.");
            return false;
        }
        modelo.Opcoes = opcoes;
        return true;
    }

    private async Task<bool> CarregarOpcoesAsync(
        EmissaoMensalidadeFormulario modelo, CancellationToken ct)
    {
        var opcoes = await ConsultarOpcoesAsync(ct);
        if (opcoes is null)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível carregar associações.");
            return false;
        }
        modelo.Opcoes = opcoes;
        return true;
    }

    private async Task<OpcoesFinanceirasApi?> ConsultarOpcoesAsync(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/financeiro/opcoes", null, ct);
            if (!resposta.IsSuccessStatusCode) return null;
            return await resposta.Content.ReadFromJsonAsync<OpcoesFinanceirasApi>(
                cancellationToken: ct);
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) { return null; }
    }

    private static async Task<string> LerErroAsync(HttpResponseMessage resposta,
        string mensagemPadrao, CancellationToken ct)
    {
        try
        {
            var erro = await resposta.Content.ReadFromJsonAsync<ErroFinanceiroApi>(
                cancellationToken: ct);
            return string.IsNullOrWhiteSpace(erro?.Erro) ? mensagemPadrao : erro.Erro;
        }
        catch (System.Text.Json.JsonException) { return mensagemPadrao; }
        catch (NotSupportedException) { return mensagemPadrao; }
    }
}
