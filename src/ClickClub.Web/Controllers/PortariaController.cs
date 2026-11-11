using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "Porteiro,AdministradorGeral")]
public sealed class PortariaController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(long? unidadeId, CancellationToken ct)
    {
        var pagina = new PortariaPaginaViewModel();
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/portaria/unidades", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!resposta.IsSuccessStatusCode)
            {
                pagina.Erro = "Validação indisponível. A API não confirmou as unidades autorizadas.";
                return View(pagina);
            }

            pagina.Unidades = await resposta.Content
                .ReadFromJsonAsync<List<UnidadePortariaApi>>(cancellationToken: ct)
                ?? [];
            if (pagina.Unidades.Count == 0)
            {
                pagina.Erro = "Nenhum clube ativo atribuído à sua conta de portaria.";
                return View(pagina);
            }

            pagina.UnidadeSelecionadaId = pagina.Unidades.Any(u => u.Id == unidadeId)
                ? unidadeId!.Value : pagina.Unidades[0].Id;

            using var historico = await api.EnviarAsync(
                HttpMethod.Get,
                $"api/portaria/registros?unidadeId={pagina.UnidadeSelecionadaId}&limite=30",
                null, ct);
            if (historico.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (historico.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!historico.IsSuccessStatusCode)
            {
                pagina.Erro = "Validação indisponível. Não foi possível consultar os acessos.";
                return View(pagina);
            }

            pagina.Historico = await historico.Content
                .ReadFromJsonAsync<List<RegistroPortariaApi>>(cancellationToken: ct)
                ?? [];

            if (TempData["ResultadoPortaria"] is string json)
            {
                var resultado = JsonSerializer
                    .Deserialize<ResultadoPortariaViewModel>(json);
                if (resultado?.UnidadeId == pagina.UnidadeSelecionadaId)
                    pagina.Resultado = resultado;
            }
            if (TempData["ErroPortaria"] is string erro)
                pagina.Erro = erro;
        }
        catch (HttpRequestException)
        {
            pagina.Resultado = null;
            pagina.Erro = "Validação indisponível: API fora do ar. Não autorize a entrada.";
        }
        catch (TaskCanceledException)
        {
            pagina.Resultado = null;
            pagina.Erro = "Validação indisponível: tempo limite da API excedido.";
        }
        catch (JsonException)
        {
            pagina.Resultado = null;
            pagina.Erro = "Validação indisponível: resposta inválida da API.";
        }

        return View(pagina);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidarQr(
        ValidacaoQrPortariaFormulario formulario, CancellationToken ct)
    {
        if (!ModelState.IsValid ||
            formulario.TipoAcesso is not ("EntradaGeral" or "UsoReserva") ||
            (formulario.TipoAcesso == "UsoReserva" && formulario.ReservaId is not > 0) ||
            (formulario.TipoAcesso == "EntradaGeral" && formulario.ReservaId is not null))
        {
            TempData["ErroPortaria"] = "Informe o QR, o tipo de acesso e a reserva quando necessária.";
            return RedirectToAction(nameof(Index), new { unidadeId = formulario.UnidadeId });
        }

        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Post, "api/portaria/validar-qr", new
                {
                    formulario.UnidadeId, formulario.CodigoQr,
                    formulario.TipoAcesso, formulario.ReservaId
                }, ct);

            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (resposta.IsSuccessStatusCode)
            {
                var decisao = await resposta.Content
                    .ReadFromJsonAsync<ValidacaoQrPortariaApi>(cancellationToken: ct);
                if (decisao is not null)
                    GuardarResultado(new ResultadoPortariaViewModel(
                        formulario.UnidadeId, decisao.RegistroId, decisao.Permitido,
                        decisao.Mensagem, decisao.MotivoCodigo,
                        decisao.NomePessoa, decisao.ValidadoEmUtc));
                else
                    TempData["ErroPortaria"] = "Validação indisponível: resposta vazia da API.";
            }
            else
            {
                TempData["ErroPortaria"] =
                    "Validação não concluída pela API. Não autorize a entrada.";
            }
        }
        catch (HttpRequestException)
        {
            TempData["ErroPortaria"] = "Validação indisponível: API fora do ar.";
        }
        catch (TaskCanceledException)
        {
            TempData["ErroPortaria"] = "Validação indisponível: tempo limite excedido.";
        }
        catch (JsonException)
        {
            TempData["ErroPortaria"] = "Validação indisponível: resposta inválida da API.";
        }

        return RedirectToAction(nameof(Index), new { unidadeId = formulario.UnidadeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidarConvidado(
        ValidacaoConvidadoPortariaFormulario formulario, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErroPortaria"] =
                "Informe a reserva e a identificação cadastrada do convidado.";
            return RedirectToAction(nameof(Index), new { unidadeId = formulario.UnidadeId });
        }

        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Post, "api/portaria/validar-convidado", new
                {
                    formulario.UnidadeId, formulario.ReservaId,
                    formulario.IdentificacaoMinima, formulario.IdentidadeConferida
                }, ct);

            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (resposta.IsSuccessStatusCode)
            {
                var decisao = await resposta.Content
                    .ReadFromJsonAsync<ValidacaoConvidadoPortariaApi>(cancellationToken: ct);
                if (decisao is not null)
                    GuardarResultado(new ResultadoPortariaViewModel(
                        formulario.UnidadeId, decisao.RegistroId, decisao.Permitido,
                        decisao.Mensagem, decisao.MotivoCodigo,
                        decisao.NomeConvidado, decisao.ValidadoEmUtc));
                else
                    TempData["ErroPortaria"] = "Validação indisponível: resposta vazia da API.";
            }
            else
            {
                TempData["ErroPortaria"] =
                    "Validação do convidado não concluída. Não autorize a entrada.";
            }
        }
        catch (HttpRequestException)
        {
            TempData["ErroPortaria"] = "Validação indisponível: API fora do ar.";
        }
        catch (TaskCanceledException)
        {
            TempData["ErroPortaria"] = "Validação indisponível: tempo limite excedido.";
        }
        catch (JsonException)
        {
            TempData["ErroPortaria"] = "Validação indisponível: resposta inválida da API.";
        }

        return RedirectToAction(nameof(Index), new { unidadeId = formulario.UnidadeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidarVisitanteEvento(
        ValidacaoVisitanteEventoPortariaFormulario formulario,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErroPortaria"] =
                "Informe o evento e a identificação cadastrada do visitante.";
            return RedirectToAction(nameof(Index), new { unidadeId = formulario.UnidadeId });
        }

        try
        {
            using var resposta = await api.EnviarAsync(HttpMethod.Post,
                "api/portaria/validar-visitante-evento", new
                {
                    formulario.UnidadeId, formulario.EventoId,
                    formulario.IdentificacaoMinima, formulario.IdentidadeConferida
                }, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (resposta.IsSuccessStatusCode)
            {
                var decisao = await resposta.Content
                    .ReadFromJsonAsync<ValidacaoConvidadoPortariaApi>(cancellationToken: ct);
                if (decisao is not null)
                    GuardarResultado(new ResultadoPortariaViewModel(
                        formulario.UnidadeId, decisao.RegistroId, decisao.Permitido,
                        decisao.Mensagem, decisao.MotivoCodigo,
                        decisao.NomeConvidado, decisao.ValidadoEmUtc));
                else
                    TempData["ErroPortaria"] =
                        "Validação indisponível: resposta vazia da API.";
            }
            else TempData["ErroPortaria"] =
                "Validação do visitante não concluída. Não autorize a entrada.";
        }
        catch (HttpRequestException)
        {
            TempData["ErroPortaria"] = "Validação indisponível: API fora do ar.";
        }
        catch (TaskCanceledException)
        {
            TempData["ErroPortaria"] = "Validação indisponível: tempo limite excedido.";
        }
        catch (JsonException)
        {
            TempData["ErroPortaria"] = "Validação indisponível: resposta inválida da API.";
        }
        return RedirectToAction(nameof(Index), new { unidadeId = formulario.UnidadeId });
    }

    private void GuardarResultado(ResultadoPortariaViewModel resultado)
    {
        // Não incluir o QR, documento ou token da sessão no TempData.
        TempData["ResultadoPortaria"] = JsonSerializer.Serialize(resultado);
    }
}
