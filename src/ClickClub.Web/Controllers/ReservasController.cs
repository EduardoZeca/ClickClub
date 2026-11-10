using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "Titular,Dependente")]
public sealed class ReservasController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    [Authorize(Roles = "Titular")]
    public async Task<IActionResult> Permissoes(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(HttpMethod.Get,
                "api/reservas/permissoes-dependentes", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden) return Forbid();
            if (!resposta.IsSuccessStatusCode)
                return Problem("Não foi possível consultar as permissões dos dependentes.");

            var lista = await resposta.Content
                .ReadFromJsonAsync<List<PermissaoReservaDependenteApi>>(
                    cancellationToken: ct) ?? [];
            return View(lista);
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
    [Authorize(Roles = "Titular")]
    public async Task<IActionResult> AlterarPermissao(
        long vinculoId, bool permitirReservaPaga, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(HttpMethod.Put,
                $"api/reservas/permissoes-dependentes/{vinculoId}",
                new { PermitirReservaPaga = permitirReservaPaga }, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden) return Forbid();
            if (resposta.IsSuccessStatusCode)
                TempData["Sucesso"] = permitirReservaPaga
                    ? "Permissão de locações pagas concedida ao dependente."
                    : "Permissão de locações pagas revogada.";
            else TempData["Erro"] = resposta.StatusCode == HttpStatusCode.NotFound
                ? "Dependente não encontrado na sua associação ativa."
                : "Não foi possível alterar a permissão.";
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível.";
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo limite da API excedido.";
        }
        return RedirectToAction(nameof(Permissoes));
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/reservas/minhas", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden) return Forbid();
            if (!resposta.IsSuccessStatusCode)
                return Problem("Não foi possível consultar suas reservas.");
            return View(await resposta.Content.ReadFromJsonAsync<List<ReservaApi>>(
                cancellationToken: ct) ?? []);
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

    [HttpGet]
    public async Task<IActionResult> Cancelar(long id, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(HttpMethod.Get,
                $"api/reservas/{id}", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden) return Forbid();
            if (resposta.StatusCode == HttpStatusCode.NotFound) return NotFound();
            if (!resposta.IsSuccessStatusCode)
                return Problem("Não foi possível consultar a reserva.");

            var reserva = await resposta.Content.ReadFromJsonAsync<ReservaApi>(
                cancellationToken: ct);
            if (reserva is null) return Problem("Resposta inválida da API.");
            if (reserva.Status != "Confirmada")
            {
                TempData["Erro"] = "Esta reserva não está confirmada e não pode ser cancelada novamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(reserva);
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
    public async Task<IActionResult> ConfirmarCancelamento(
        long reservaId, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(HttpMethod.Post,
                $"api/reservas/{reservaId}/cancelamento", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden) return Forbid();
            if (resposta.StatusCode == HttpStatusCode.NotFound) return NotFound();

            if (resposta.IsSuccessStatusCode)
            {
                var resultado = await resposta.Content
                    .ReadFromJsonAsync<CancelamentoReservaApi>(cancellationToken: ct);
                if (resultado is null) return Problem("Resposta inválida da API.");
                var cultura = CultureInfo.GetCultureInfo("pt-BR");
                TempData["Sucesso"] =
                    $"Reserva #{resultado.ReservaId} cancelada. " +
                    $"Multa simulada: {resultado.MultaSimulada.ToString("C", cultura)}. " +
                    $"Crédito simulado pendente: {resultado.CreditoSimuladoPendente.ToString("C", cultura)}. " +
                    $"Situação financeira: {resultado.SituacaoFinanceira}.";
                return RedirectToAction(nameof(Index));
            }

            if (resposta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                var erro = await resposta.Content.ReadFromJsonAsync<ErroApi>(
                    cancellationToken: ct);
                TempData["Erro"] = erro?.Erro ?? "Cancelamento não permitido.";
                return RedirectToAction(nameof(Index));
            }
            return Problem("Não foi possível cancelar a reserva.");
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

    [HttpGet]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var modelo = new ReservaFormulario();
        await PrepararEspacosAsync(modelo, ct);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Criar(
        ReservaFormulario modelo, CancellationToken ct)
    {
        if (modelo.InicioLocal is not null && modelo.FimLocal is not null &&
            modelo.FimLocal <= modelo.InicioLocal)
            ModelState.AddModelError(string.Empty,
                "O fim da utilização deve ser posterior ao início.");

        if (!ConversorConvidados.TentarLer(modelo.ConvidadosTexto,
                out var convidados))
            ModelState.AddModelError(nameof(modelo.ConvidadosTexto),
                "Informe um convidado por linha: Nome - Identificação ou Nome | Identificação.");

        if (ModelState.IsValid)
        {
            try
            {
                using var resposta = await api.EnviarAsync(
                    HttpMethod.Post, "api/reservas", new
                    {
                        modelo.EspacoId, modelo.InicioLocal,
                        modelo.FimLocal, modelo.QuantidadeParticipantes,
                        modelo.Finalidade, Convidados = convidados,
                        modelo.ListaConvidadosCompleta
                    }, ct);
                if (resposta.StatusCode == HttpStatusCode.Created)
                {
                    TempData["Sucesso"] = "Reserva confirmada. Confira a situação da taxa simulada.";
                    return RedirectToAction(nameof(Index));
                }
                if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToAction("Entrar", "Conta");
                if (resposta.StatusCode == HttpStatusCode.Forbidden)
                    return Forbid();
                if (resposta.StatusCode is HttpStatusCode.BadRequest or
                    HttpStatusCode.Conflict or HttpStatusCode.NotFound)
                {
                    var erro = await resposta.Content
                        .ReadFromJsonAsync<ErroApi>(cancellationToken: ct);
                    ModelState.AddModelError(string.Empty,
                        erro?.Erro ?? "A reserva não foi aceita.");
                }
                else ModelState.AddModelError(string.Empty,
                    "Não foi possível confirmar a reserva.");
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

        await PrepararEspacosAsync(modelo, ct);
        return View(modelo);
    }

    [HttpGet]
    public Task<IActionResult> Convidados(long id, CancellationToken ct) =>
        ExibirConvidadosAsync(new ConvidadosReservaFormulario
        {
            ReservaId = id
        }, ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarConvidados(
        ConvidadosReservaFormulario modelo, CancellationToken ct)
    {
        if (!ConversorConvidados.TentarLer(modelo.ConvidadosTexto,
                out var convidados))
            ModelState.AddModelError(nameof(modelo.ConvidadosTexto),
                "Informe um convidado por linha: Nome - Identificação ou Nome | Identificação.");

        if (ModelState.IsValid)
        {
            try
            {
                using var resposta = await api.EnviarAsync(
                    HttpMethod.Put,
                    $"api/reservas/{modelo.ReservaId}/convidados",
                    new { Convidados = convidados }, ct);

                if (resposta.StatusCode == HttpStatusCode.NoContent)
                {
                    TempData["Sucesso"] = "Lista de convidados atualizada.";
                    return RedirectToAction(nameof(Convidados),
                        new { id = modelo.ReservaId });
                }
                if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToAction("Entrar", "Conta");
                if (resposta.StatusCode == HttpStatusCode.Forbidden) return Forbid();
                if (resposta.StatusCode == HttpStatusCode.NotFound) return NotFound();

                if (resposta.StatusCode is HttpStatusCode.BadRequest or
                    HttpStatusCode.Conflict)
                {
                    var erro = await resposta.Content.ReadFromJsonAsync<ErroApi>(
                        cancellationToken: ct);
                    ModelState.AddModelError(string.Empty,
                        erro?.Erro ?? "Não foi possível atualizar os convidados.");
                }
                else ModelState.AddModelError(string.Empty,
                    "Não foi possível atualizar os convidados.");
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

        return await ExibirConvidadosAsync(modelo, ct);
    }

    private async Task<IActionResult> ExibirConvidadosAsync(
        ConvidadosReservaFormulario modelo, CancellationToken ct)
    {
        try
        {
            using var reservaResposta = await api.EnviarAsync(HttpMethod.Get,
                $"api/reservas/{modelo.ReservaId}", null, ct);
            if (reservaResposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (reservaResposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (reservaResposta.StatusCode == HttpStatusCode.NotFound)
                return NotFound();
            if (!reservaResposta.IsSuccessStatusCode)
                return Problem("Não foi possível consultar a reserva.");

            modelo.Reserva = await reservaResposta.Content.ReadFromJsonAsync<ReservaApi>(
                cancellationToken: ct);
            using var listaResposta = await api.EnviarAsync(HttpMethod.Get,
                $"api/reservas/{modelo.ReservaId}/convidados", null, ct);
            if (listaResposta.StatusCode == HttpStatusCode.NotFound)
                return NotFound();
            if (!listaResposta.IsSuccessStatusCode)
                return Problem("Não foi possível consultar os convidados.");

            modelo.EstadoConvidados = await listaResposta.Content
                .ReadFromJsonAsync<ConvidadosReservaApi>(cancellationToken: ct);
            if (modelo.EstadoConvidados is null || modelo.Reserva is null)
                return Problem("Resposta inválida da API.");

            modelo.ConvidadosTexto ??=
                ConversorConvidados.ParaTexto(modelo.EstadoConvidados.Itens);
            return View("Convidados", modelo);
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

    private async Task PrepararEspacosAsync(
        ReservaFormulario modelo, CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/reservas/espacos", null, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty,
                    "Não foi possível consultar os espaços disponíveis.");
                return;
            }
            modelo.Espacos = await resposta.Content
                .ReadFromJsonAsync<List<EspacoReservaOpcaoApi>>(
                    cancellationToken: ct) ?? [];
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
}
