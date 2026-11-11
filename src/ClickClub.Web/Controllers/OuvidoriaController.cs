using System.Net;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize]
public sealed class OuvidoriaController(ClickClubApiClient api) : Controller
{
    private readonly ModuloApiWeb _cliente = new(api);

    private bool Associado => User.IsInRole("Titular") || User.IsInRole("Dependente");
    private bool Gestor => User.IsInRole("AdministradorGeral") ||
        User.IsInRole("PresidenteClube") || User.IsInRole("FuncionarioUnidade");

    [HttpGet]
    public IActionResult Index() => Associado
        ? RedirectToAction(nameof(Minhas))
        : Gestor ? RedirectToAction(nameof(Gerenciar)) : Forbid();

    [HttpGet]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Minhas(CancellationToken ct)
    {
        var (codigo, itens, erro) = await _cliente.ObterAsync<List<ChamadoOuvidoriaApi>>(
            "api/ouvidoria/minhas", ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        return codigo == HttpStatusCode.OK
            ? View(itens!) : Problem(erro ?? "Não foi possível consultar seus chamados.");
    }

    [HttpGet]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var formulario = new ChamadoOuvidoriaFormulario();
        await CarregarClubesAsync(formulario, ct);
        return View("Formulario", formulario);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Salvar(
        ChamadoOuvidoriaFormulario modelo, CancellationToken ct)
    {
        if (modelo.UnidadeId is <= 0)
            ModelState.AddModelError(nameof(modelo.UnidadeId), "Clube inválido.");
        if (ModelState.IsValid)
        {
            var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Post,
                "api/ouvidoria", new
                {
                    modelo.UnidadeId, modelo.Tipo, modelo.Categoria,
                    modelo.Assunto, modelo.Descricao
                }, ct);
            if ((int)codigo is >= 200 and < 300)
            {
                TempData["Sucesso"] = "Chamado registrado na ouvidoria.";
                return RedirectToAction(nameof(Minhas));
            }
            if (codigo == HttpStatusCode.Forbidden) return Forbid();
            ModelState.AddModelError(string.Empty,
                erro ?? "Não foi possível registrar o chamado.");
        }
        await CarregarClubesAsync(modelo, ct);
        return View("Formulario", modelo);
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Gerenciar(CancellationToken ct)
    {
        var (codigo, itens, erro) = await _cliente.ObterAsync<List<ChamadoOuvidoriaApi>>(
            "api/ouvidoria/gerenciar", ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        return codigo == HttpStatusCode.OK
            ? View(itens!) : Problem(erro ?? "Não foi possível consultar a ouvidoria do clube.");
    }

    [HttpGet]
    [Authorize(Roles = "Titular,Dependente,AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken ct)
    {
        var (codigo, detalhe, erro) =
            await _cliente.ObterAsync<ChamadoOuvidoriaDetalheApi>(
                $"api/ouvidoria/{id}", ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        if (codigo == HttpStatusCode.NotFound) return NotFound();
        return codigo == HttpStatusCode.OK
            ? View(detalhe!) : Problem(erro ?? "Chamado indisponível.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Responder(
        RespostaOuvidoriaFormulario modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Erro"] = "Informe uma resposta válida, com ao menos 2 caracteres.";
            return RedirectToAction(nameof(Detalhes), new { id = modelo.Id });
        }
        var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Post,
            $"api/ouvidoria/{modelo.Id}/respostas",
            new { modelo.Conteudo }, ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        TempData[(int)codigo is >= 200 and < 300 ? "Sucesso" : "Erro"] =
            (int)codigo is >= 200 and < 300
                ? "Resposta registrada no histórico do chamado." : erro;
        return RedirectToAction(nameof(Detalhes), new { id = modelo.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Encerrar(long id, CancellationToken ct)
    {
        var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Post,
            $"api/ouvidoria/{id}/encerrar", null, ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        TempData[(int)codigo is >= 200 and < 300 ? "Sucesso" : "Erro"] =
            (int)codigo is >= 200 and < 300
                ? "Chamado encerrado e histórico preservado." : erro;
        return RedirectToAction(nameof(Detalhes), new { id });
    }

    private async Task CarregarClubesAsync(
        ChamadoOuvidoriaFormulario modelo, CancellationToken ct)
    {
        var (codigo, clubes, erro) = await _cliente.ObterAsync<List<ClubeOuvidoriaApi>>(
            "api/ouvidoria/clubes", ct);
        modelo.Clubes = clubes ?? [];
        if (codigo != HttpStatusCode.OK)
            ModelState.AddModelError(string.Empty,
                erro ?? "Não foi possível consultar os clubes ativos.");
    }
}
