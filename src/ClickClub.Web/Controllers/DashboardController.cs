using System.Net;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade,Tesoureiro")]
public sealed class DashboardController(ClickClubApiClient api) : Controller
{
    private readonly ModuloApiWeb _cliente = new(api);

    [HttpGet]
    public async Task<IActionResult> Index(
        DateOnly? inicio, DateOnly? fim, long? unidadeId, CancellationToken ct)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var dataInicio = inicio ?? new DateOnly(hoje.Year, hoje.Month, 1);
        var dataFim = fim ?? hoje;
        var pagina = new DashboardPagina
        {
            Inicio = dataInicio, Fim = dataFim, UnidadeId = unidadeId
        };
        if (!ModelState.IsValid || dataInicio > dataFim ||
            dataFim == DateOnly.MaxValue || unidadeId is <= 0)
        {
            pagina.Erro = "Informe datas válidas e um clube positivo.";
            return View(pagina);
        }

        var rota = $"api/dashboard?inicio={dataInicio:yyyy-MM-dd}" +
            $"&fim={dataFim:yyyy-MM-dd}" +
            (unidadeId.HasValue ? $"&unidadeId={unidadeId.Value}" : "");
        var (codigo, dados, erro) = await _cliente.ObterAsync<DashboardApi>(rota, ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        if (codigo == HttpStatusCode.Unauthorized) return Challenge();
        pagina.Resumo = dados;
        pagina.Erro = codigo == HttpStatusCode.OK
            ? null : erro ?? "Não foi possível consultar o dashboard.";
        return View(pagina);
    }
}
