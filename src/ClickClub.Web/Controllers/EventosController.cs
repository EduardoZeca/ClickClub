using System.Net;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

public sealed class EventosController(ClickClubApiClient api) : Controller
{
    private readonly ModuloApiWeb _cliente = new(api);

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var (codigo, itens, erro) = await _cliente.ObterAsync<List<EventoSocialApi>>(
            "api/eventos", ct);
        return codigo == HttpStatusCode.OK
            ? View(itens!) : Problem(erro ?? "Não foi possível consultar os eventos.");
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Gerenciar(CancellationToken ct)
    {
        var (codigo, itens, erro) = await _cliente.ObterAsync<List<EventoSocialApi>>(
            "api/eventos/gerenciar", ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        return codigo == HttpStatusCode.OK
            ? View(itens!) : Problem(erro ?? "Não foi possível consultar os eventos do clube.");
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var modelo = new EventoFormulario();
        await CarregarClubesAsync(modelo, ct);
        return View("Formulario", modelo);
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Editar(long id, CancellationToken ct)
    {
        var (codigo, evento, erro) = await _cliente.ObterAsync<EventoSocialApi>(
            $"api/eventos/{id}", ct);
        if (codigo == HttpStatusCode.NotFound) return NotFound();
        if (codigo != HttpStatusCode.OK)
            return Problem(erro ?? "Evento não disponível.");
        var modelo = EventoFormulario.De(evento!);
        await CarregarClubesAsync(modelo, ct);
        if (!modelo.Clubes.Any(c => c.Id == modelo.UnidadeId)) return Forbid();
        return View("Formulario", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Salvar(EventoFormulario modelo, CancellationToken ct)
    {
        if (modelo.Tipo == "Torneio" && !modelo.Capacidade.HasValue)
            ModelState.AddModelError(nameof(modelo.Capacidade),
                "Torneio exige capacidade definida.");
        if (ModelState.IsValid)
        {
            var metodo = modelo.Id == 0 ? HttpMethod.Post : HttpMethod.Put;
            var rota = modelo.Id == 0
                ? "api/eventos" : $"api/eventos/{modelo.Id}";
            var (codigo, erro) = await _cliente.AlterarAsync(metodo, rota, new
            {
                modelo.UnidadeId, modelo.Nome, modelo.Descricao,
                modelo.InicioLocal, modelo.FimLocal,
                modelo.PublicoAlvo, modelo.Tipo, modelo.Capacidade
            }, ct);
            if ((int)codigo is >= 200 and < 300)
            {
                TempData["Sucesso"] = "Evento salvo.";
                return RedirectToAction(nameof(Gerenciar));
            }
            if (codigo == HttpStatusCode.Forbidden) return Forbid();
            ModelState.AddModelError(string.Empty, erro ?? "Evento não salvo.");
        }
        await CarregarClubesAsync(modelo, ct);
        return View("Formulario", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> AlterarSituacao(long id, string status,
        CancellationToken ct)
    {
        if (status is not ("Publicado" or "Encerrado" or "Cancelado"))
            return BadRequest();
        var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Patch,
            $"api/eventos/{id}/situacao", new { status }, ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        TempData[(int)codigo is >= 200 and < 300 ? "Sucesso" : "Erro"] =
            (int)codigo is >= 200 and < 300
                ? "Situação do evento atualizada." : erro;
        return RedirectToAction(nameof(Gerenciar));
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Inscritos(long id, CancellationToken ct)
    {
        var (ce, evento, erroE) = await _cliente.ObterAsync<EventoSocialApi>(
            $"api/eventos/{id}", ct);
        if (ce != HttpStatusCode.OK)
            return Problem(erroE ?? "Não foi possível consultar o evento.");
        var (ci, inscritos, erroI) = await _cliente.ObterAsync<List<InscricaoEventoApi>>(
            $"api/eventos/{id}/inscricoes", ct);
        if (ci == HttpStatusCode.Forbidden) return Forbid();
        if (ci != HttpStatusCode.OK)
            return Problem(erroI ?? "Não foi possível consultar inscrições.");
        return View(new EventoInscritosWeb(evento!, inscritos!));
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Visitantes(long id, CancellationToken ct)
    {
        var (ce, evento, erroE) = await _cliente.ObterAsync<EventoSocialApi>(
            $"api/eventos/{id}", ct);
        if (ce != HttpStatusCode.OK)
            return Problem(erroE ?? "Não foi possível consultar o evento.");
        var (cv, visitantes, erroV) = await _cliente.ObterAsync<List<VisitanteEventoApi>>(
            $"api/eventos/{id}/visitantes", ct);
        if (cv == HttpStatusCode.Forbidden) return Forbid();
        if (cv != HttpStatusCode.OK)
            return Problem(erroV ?? "Não foi possível consultar visitantes.");
        return View(new VisitantesEventoWeb(evento!, visitantes!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> CadastrarVisitante(
        VisitanteEventoFormulario formulario, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Erro"] = "Informe nome e identificação do visitante.";
            return RedirectToAction(nameof(Visitantes), new { id = formulario.EventoId });
        }
        var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Post,
            $"api/eventos/{formulario.EventoId}/visitantes",
            new { formulario.Nome, formulario.IdentificacaoMinima }, ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        TempData[(int)codigo is >= 200 and < 300 ? "Sucesso" : "Erro"] =
            (int)codigo is >= 200 and < 300
                ? "Visitante pré-autorizado. A entrada ainda depende da portaria online."
                : erro;
        return RedirectToAction(nameof(Visitantes), new { id = formulario.EventoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> AlterarVisitante(long eventoId,
        long visitanteId, string situacao, CancellationToken ct)
    {
        if (situacao is not ("Autorizado" or "Revogado")) return BadRequest();
        var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Patch,
            $"api/eventos/{eventoId}/visitantes/{visitanteId}/situacao",
            new { situacao }, ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        TempData[(int)codigo is >= 200 and < 300 ? "Sucesso" : "Erro"] =
            (int)codigo is >= 200 and < 300
                ? "Situação do visitante atualizada." : erro;
        return RedirectToAction(nameof(Visitantes), new { id = eventoId });
    }

    [HttpGet]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Minhas(CancellationToken ct)
    {
        var (codigo, itens, erro) =
            await _cliente.ObterAsync<List<MinhaInscricaoEventoApi>>(
                "api/eventos/minhas-inscricoes", ct);
        return codigo == HttpStatusCode.OK
            ? View(itens!) : Problem(erro ?? "Não foi possível consultar suas inscrições.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Inscrever(long id, CancellationToken ct)
    {
        var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Post,
            $"api/eventos/{id}/inscricoes", null, ct);
        TempData[(int)codigo is >= 200 and < 300 ? "Sucesso" : "Erro"] =
            (int)codigo is >= 200 and < 300 ? "Inscrição no evento registrada." : erro;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Cancelar(long id, CancellationToken ct)
    {
        var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Delete,
            $"api/eventos/{id}/inscricoes/minha", null, ct);
        TempData[(int)codigo is >= 200 and < 300 ? "Sucesso" : "Erro"] =
            (int)codigo is >= 200 and < 300 ? "Inscrição cancelada." : erro;
        return RedirectToAction(nameof(Minhas));
    }

    private async Task CarregarClubesAsync(EventoFormulario modelo, CancellationToken ct)
    {
        var (codigo, clubes, erro) =
            await _cliente.ObterAsync<List<ClubeTurmaOpcaoApi>>(
                "api/turmas/clubes-gerenciaveis", ct);
        if (codigo != HttpStatusCode.OK)
            ModelState.AddModelError(string.Empty,
                erro ?? "Não foi possível carregar os clubes atribuídos.");
        modelo.Clubes = clubes ?? [];
    }
}
