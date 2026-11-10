using System.Net;
using System.Net.Http.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "AdministradorGeral,FuncionarioUnidade,PresidenteClube")]
public sealed class EspacosAgendaController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Detalhes(
        long id, int? ano, int? mes, CancellationToken ct)
    {
        var referencia = DateTime.Today;
        var anoConsulta = ano ?? referencia.Year;
        var mesConsulta = mes ?? referencia.Month;
        if (anoConsulta is < 2000 or > 2100 || mesConsulta is < 1 or > 12)
            return BadRequest("Mês ou ano inválido.");

        try
        {
            using var dados = await api.EnviarAsync(
                HttpMethod.Get, $"api/espacos/{id}", null, ct);
            if (dados.StatusCode == HttpStatusCode.NotFound) return NotFound();
            if (dados.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (dados.StatusCode == HttpStatusCode.Forbidden) return Forbid();
            if (!dados.IsSuccessStatusCode)
                return Problem("Não foi possível obter o espaço.");
            var espaco = await dados.Content.ReadFromJsonAsync<EspacoApi>(
                cancellationToken: ct);
            if (espaco is null) return Problem("Resposta inválida da API.");

            using var horariosResposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/espacos/{id}/horarios", null, ct);
            using var manutencoesResposta = await api.EnviarAsync(
                HttpMethod.Get, $"api/espacos/{id}/manutencoes", null, ct);
            using var ocupacoesResposta = await api.EnviarAsync(
                HttpMethod.Get,
                $"api/espacos/{id}/ocupacoes?ano={anoConsulta}&mes={mesConsulta}",
                null, ct);
            if (horariosResposta.StatusCode == HttpStatusCode.Forbidden ||
                manutencoesResposta.StatusCode == HttpStatusCode.Forbidden ||
                ocupacoesResposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (!horariosResposta.IsSuccessStatusCode ||
                !manutencoesResposta.IsSuccessStatusCode ||
                !ocupacoesResposta.IsSuccessStatusCode)
                return Problem("Não foi possível obter a agenda do espaço.");

            var horarios = await horariosResposta.Content
                .ReadFromJsonAsync<List<HorarioEspacoApi>>(cancellationToken: ct);
            var manutencoes = await manutencoesResposta.Content
                .ReadFromJsonAsync<List<ManutencaoEspacoApi>>(cancellationToken: ct);
            var ocupacoes = await ocupacoesResposta.Content
                .ReadFromJsonAsync<List<OcupacaoEspacoApi>>(cancellationToken: ct);
            return View(new AgendaEspacoPagina(
                espaco, horarios ?? [], manutencoes ?? [],
                anoConsulta, mesConsulta, ocupacoes ?? []));
        }
        catch (HttpRequestException)
        {
            return Problem("API indisponível.");
        }
        catch (TaskCanceledException)
        {
            return Problem("Tempo limite de comunicação com a API.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarHorario(
        HorarioEspacoFormulario modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Erro"] = "Informe dia e horas no formato HH:mm.";
            return RedirectToAction(nameof(Detalhes), new { id = modelo.EspacoId });
        }
        return await ExecutarAsync(HttpMethod.Post,
            $"api/espacos/{modelo.EspacoId}/horarios", new
            {
                modelo.DiaSemana, modelo.Inicio, modelo.Fim,
                modelo.FimDiaSeguinte
            }, modelo.EspacoId, "Horário cadastrado.", ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarHorario(
        long espacoId, long horarioId, bool ativo, CancellationToken ct) =>
        await ExecutarAsync(HttpMethod.Patch,
            $"api/espacos/{espacoId}/horarios/{horarioId}/situacao",
            new { ativo }, espacoId, "Situação do horário atualizada.", ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarManutencao(
        ManutencaoEspacoFormulario modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid || modelo.FimLocal <= modelo.InicioLocal)
        {
            TempData["Erro"] = "Informe motivo e período de manutenção válidos.";
            return RedirectToAction(nameof(Detalhes), new { id = modelo.EspacoId });
        }
        return await ExecutarAsync(HttpMethod.Post,
            $"api/espacos/{modelo.EspacoId}/manutencoes", new
            {
                modelo.InicioLocal, modelo.FimLocal, modelo.Motivo
            }, modelo.EspacoId, "Bloqueio de manutenção cadastrado.", ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarManutencao(
        long espacoId, long manutencaoId, bool ativo, CancellationToken ct) =>
        await ExecutarAsync(HttpMethod.Patch,
            $"api/espacos/{espacoId}/manutencoes/{manutencaoId}/situacao",
            new { ativo }, espacoId,
            "Situação da manutenção atualizada.", ct);

    private async Task<IActionResult> ExecutarAsync(HttpMethod metodo,
        string rota, object dados, long espacoId, string sucesso,
        CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(metodo, rota, dados, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden) return Forbid();
            if (resposta.IsSuccessStatusCode)
                TempData["Sucesso"] = sucesso;
            else if (resposta.StatusCode is HttpStatusCode.Conflict or
                     HttpStatusCode.BadRequest)
            {
                var erro = await resposta.Content
                    .ReadFromJsonAsync<ErroApi>(cancellationToken: ct);
                TempData["Erro"] = erro?.Erro ?? "A operação não foi aceita.";
            }
            else TempData["Erro"] = "Não foi possível atualizar a agenda.";
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível.";
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo limite de comunicação com a API.";
        }
        return RedirectToAction(nameof(Detalhes), new { id = espacoId });
    }
}
