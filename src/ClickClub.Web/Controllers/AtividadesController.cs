using System.Net;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
public sealed class AtividadesController(ClickClubApiClient api) : Controller
{
    private readonly ModuloApiWeb _cliente = new(api);

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var (codigo, itens, erro) = await _cliente.ObterAsync<List<AtividadeApi>>(
            "api/atividades", ct);
        if (codigo == HttpStatusCode.Unauthorized)
            return RedirectToAction("Entrar", "Conta");
        return codigo == HttpStatusCode.OK
            ? View(itens!) : Problem(erro ?? "Falha ao consultar atividades.");
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral")]
    public IActionResult Criar() => View("Formulario", new AtividadeFormulario());

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral")]
    public async Task<IActionResult> Editar(long id, CancellationToken ct)
    {
        var (codigo, item, erro) = await _cliente.ObterAsync<AtividadeApi>(
            $"api/atividades/{id}", ct);
        if (codigo == HttpStatusCode.Unauthorized)
            return RedirectToAction("Entrar", "Conta");
        if (codigo == HttpStatusCode.NotFound) return NotFound();
        return codigo == HttpStatusCode.OK
            ? View("Formulario", AtividadeFormulario.De(item!))
            : Problem(erro ?? "Falha ao consultar atividade.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral")]
    public async Task<IActionResult> Salvar(AtividadeFormulario modelo,
        CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var rota = modelo.Id == 0
                ? "api/atividades" : $"api/atividades/{modelo.Id}";
            var metodo = modelo.Id == 0 ? HttpMethod.Post : HttpMethod.Put;
            var (codigo, erro) = await _cliente.AlterarAsync(metodo, rota, new
            {
                modelo.Nome, modelo.Descricao, modelo.Tipo
            }, ct);
            if ((int)codigo is >= 200 and < 300)
            {
                TempData["Sucesso"] = "Atividade salva.";
                return RedirectToAction(nameof(Index));
            }
            if (codigo == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (codigo == HttpStatusCode.Forbidden) return Forbid();
            ModelState.AddModelError(string.Empty, erro ?? "Atividade não salva.");
        }
        return View("Formulario", modelo);
    }
}
