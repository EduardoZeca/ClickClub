using System.Net;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize]
public sealed class AvisosController(ClickClubApiClient api) : Controller
{
    private readonly ModuloApiWeb _cliente = new(api);

    private bool Administrador => User.IsInRole("AdministradorGeral");

    [HttpGet]
    public async Task<IActionResult> Index(long? unidadeId, CancellationToken ct)
    {
        if (unidadeId is <= 0) return BadRequest();
        var rota = unidadeId.HasValue
            ? $"api/avisos?unidadeId={unidadeId.Value}"
            : "api/avisos";
        var (codigo, itens, erro) = await _cliente.ObterAsync<List<AvisoApi>>(rota, ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        if (codigo == HttpStatusCode.Unauthorized)
            return RedirectToAction("Entrar", "Conta");
        return codigo == HttpStatusCode.OK
            ? View(itens!) : Problem(erro ?? "Mural indisponível.");
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Gerenciar(CancellationToken ct)
    {
        var (codigo, itens, erro) = await _cliente.ObterAsync<List<AvisoApi>>(
            "api/avisos/gerenciar", ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        return codigo == HttpStatusCode.OK
            ? View(itens!) : Problem(erro ?? "Não foi possível consultar os avisos sob sua gestão.");
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var modelo = new AvisoFormulario
        {
            InicioUtc = DateTime.UtcNow.AddMinutes(5),
            FimUtc = DateTime.UtcNow.AddDays(7)
        };
        await CarregarClubesAsync(modelo, ct);
        return View("Formulario", modelo);
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Editar(long id, CancellationToken ct)
    {
        var (codigo, aviso, erro) = await _cliente.ObterAsync<AvisoApi>(
            $"api/avisos/{id}", ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        if (codigo == HttpStatusCode.NotFound) return NotFound();
        if (codigo != HttpStatusCode.OK)
            return Problem(erro ?? "Aviso indisponível.");
        var modelo = AvisoFormulario.De(aviso!);
        await CarregarClubesAsync(modelo, ct);
        return View("Formulario", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Salvar(AvisoFormulario modelo,
        CancellationToken ct)
    {
        if (!Administrador && !modelo.UnidadeId.HasValue)
            ModelState.AddModelError(nameof(modelo.UnidadeId),
                "Selecione um clube. Apenas o Administrador Geral publica avisos gerais.");
        if (modelo.InicioUtc.HasValue && modelo.FimUtc.HasValue &&
            modelo.FimUtc <= modelo.InicioUtc)
            ModelState.AddModelError(nameof(modelo.FimUtc),
                "O término deve ocorrer depois do início.");
        if (ModelState.IsValid)
        {
            var metodo = modelo.Id == 0 ? HttpMethod.Post : HttpMethod.Put;
            var rota = modelo.Id == 0
                ? "api/avisos" : $"api/avisos/{modelo.Id}";
            var (codigo, erro) = await _cliente.AlterarAsync(metodo, rota, new
            {
                modelo.UnidadeId, modelo.Titulo, modelo.Corpo,
                modelo.InicioUtc, modelo.FimUtc
            }, ct);
            if ((int)codigo is >= 200 and < 300)
            {
                TempData["Sucesso"] = "Aviso salvo no mural.";
                return RedirectToAction(nameof(Gerenciar));
            }
            if (codigo == HttpStatusCode.Forbidden) return Forbid();
            ModelState.AddModelError(string.Empty, erro ?? "Aviso não salvo.");
        }
        await CarregarClubesAsync(modelo, ct);
        return View("Formulario", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> AlterarSituacao(long id, bool ativo,
        CancellationToken ct)
    {
        var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Patch,
            $"api/avisos/{id}/situacao", new { ativo }, ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        TempData[(int)codigo is >= 200 and < 300 ? "Sucesso" : "Erro"] =
            (int)codigo is >= 200 and < 300
                ? (ativo ? "Aviso reativado." : "Aviso retirado do mural.")
                : erro;
        return RedirectToAction(nameof(Gerenciar));
    }

    private async Task CarregarClubesAsync(AvisoFormulario modelo,
        CancellationToken ct)
    {
        var (codigo, clubes, erro) =
            await _cliente.ObterAsync<List<ClubeAvisoApi>>(
                "api/avisos/clubes-gerenciaveis", ct);
        modelo.Clubes = clubes ?? [];
        if (codigo != HttpStatusCode.OK)
            ModelState.AddModelError(string.Empty,
                erro ?? "Não foi possível carregar os clubes atribuídos.");
    }
}
