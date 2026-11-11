using System.Net;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

[Authorize]
public sealed class TurmasController(ClickClubApiClient api) : Controller
{
    private readonly ModuloApiWeb _cliente = new(api);

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var (codigo, turmas, erro) = await _cliente.ObterAsync<List<TurmaApi>>(
            "api/turmas", ct);
        if (codigo == HttpStatusCode.Unauthorized)
            return RedirectToAction("Entrar", "Conta");
        if (codigo != HttpStatusCode.OK)
            return Problem(erro ?? "Não foi possível carregar as turmas.");
        var linhas = new List<TurmaLinhaWeb>();
        foreach (var turma in turmas!)
        {
            var (status, vagas, _) = await _cliente.ObterAsync<VagasTurmaApi>(
                $"api/turmas/{turma.Id}/vagas", ct);
            linhas.Add(new(turma, status == HttpStatusCode.OK ? vagas : null));
        }
        return View(linhas);
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var modelo = new TurmaFormulario();
        await CarregarOpcoesAsync(modelo, ct);
        return View("Formulario", modelo);
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Editar(long id, CancellationToken ct)
    {
        var (codigo, turma, erro) = await _cliente.ObterAsync<TurmaApi>(
            $"api/turmas/{id}", ct);
        if (codigo == HttpStatusCode.Unauthorized)
            return RedirectToAction("Entrar", "Conta");
        if (codigo == HttpStatusCode.NotFound) return NotFound();
        if (codigo != HttpStatusCode.OK)
            return Problem(erro ?? "Não foi possível carregar a turma.");
        var modelo = TurmaFormulario.De(turma!);
        await CarregarOpcoesAsync(modelo, ct);
        if (!modelo.Clubes.Any(c => c.Id == modelo.UnidadeId)) return Forbid();
        return View("Formulario", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Salvar(TurmaFormulario modelo, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var metodo = modelo.Id == 0 ? HttpMethod.Post : HttpMethod.Put;
            var rota = modelo.Id == 0
                ? "api/turmas" : $"api/turmas/{modelo.Id}";
            var (codigo, erro) = await _cliente.AlterarAsync(metodo, rota, new
            {
                modelo.AtividadeId, modelo.UnidadeId,
                modelo.InstrutorPessoaId, modelo.InicioLocal,
                modelo.FimLocal, modelo.Capacidade
            }, ct);
            if ((int)codigo is >= 200 and < 300)
            {
                TempData["Sucesso"] = "Turma salva.";
                return RedirectToAction(nameof(Index));
            }
            if (codigo == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (codigo == HttpStatusCode.Forbidden) return Forbid();
            ModelState.AddModelError(string.Empty, erro ?? "Turma não salva.");
        }
        await CarregarOpcoesAsync(modelo, ct);
        return View("Formulario", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> AlterarSituacao(long id, string status,
        CancellationToken ct)
    {
        if (status is not ("Aberta" or "Encerrada" or "Cancelada"))
            return BadRequest();
        var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Patch,
            $"api/turmas/{id}/situacao", new { status }, ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        TempData[(int)codigo is >= 200 and < 300 ? "Sucesso" : "Erro"] =
            (int)codigo is >= 200 and < 300 ? "Situação da turma atualizada." : erro;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Inscricoes(long id, CancellationToken ct)
    {
        var (cTurma, turma, erroTurma) = await _cliente.ObterAsync<TurmaApi>(
            $"api/turmas/{id}", ct);
        if (cTurma != HttpStatusCode.OK)
            return Problem(erroTurma ?? "Não foi possível consultar a turma.");
        var (codigo, itens, erro) = await _cliente.ObterAsync<List<InscricaoTurmaDetalheApi>>(
            $"api/turmas/{id}/inscricoes", ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        if (codigo != HttpStatusCode.OK)
            return Problem(erro ?? "Não foi possível consultar a lista de inscritos.");
        var (cv, vagas, _) = await _cliente.ObterAsync<VagasTurmaApi>(
            $"api/turmas/{id}/vagas", ct);
        return View(new TurmaGestaoWeb(turma!, itens!,
            cv == HttpStatusCode.OK ? vagas : null));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Promover(long id, CancellationToken ct)
    {
        var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Post,
            $"api/turmas/{id}/promover-proximo", null, ct);
        if (codigo == HttpStatusCode.Forbidden) return Forbid();
        TempData[(int)codigo is >= 200 and < 300 ? "Sucesso" : "Erro"] =
            (int)codigo is >= 200 and < 300
                ? "Primeiro inscrito elegível promovido." : erro;
        return RedirectToAction(nameof(Inscricoes), new { id });
    }

    [HttpGet]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Minhas(CancellationToken ct)
    {
        var (codigo, itens, erro) =
            await _cliente.ObterAsync<List<MinhaInscricaoTurmaApi>>(
                "api/turmas/minhas-inscricoes", ct);
        if (codigo == HttpStatusCode.Unauthorized)
            return RedirectToAction("Entrar", "Conta");
        return codigo == HttpStatusCode.OK
            ? View(itens!) : Problem(erro ?? "Não foi possível consultar suas inscrições.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Inscrever(long id, CancellationToken ct)
    {
        var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Post,
            $"api/turmas/{id}/inscricoes", null, ct);
        TempData[(int)codigo is >= 200 and < 300 ? "Sucesso" : "Erro"] =
            (int)codigo is >= 200 and < 300
                ? "Inscrição registrada. Consulte a situação em Minhas inscrições."
                : erro;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Cancelar(long id, CancellationToken ct)
    {
        var (codigo, erro) = await _cliente.AlterarAsync(HttpMethod.Delete,
            $"api/turmas/{id}/inscricoes/minha", null, ct);
        TempData[(int)codigo is >= 200 and < 300 ? "Sucesso" : "Erro"] =
            (int)codigo is >= 200 and < 300 ? "Inscrição cancelada." : erro;
        return RedirectToAction(nameof(Minhas));
    }

    private async Task CarregarOpcoesAsync(TurmaFormulario modelo,
        CancellationToken ct)
    {
        var (ca, atividades, erroA) = await _cliente.ObterAsync<List<AtividadeApi>>(
            "api/atividades", ct);
        var (cc, clubes, erroC) = await _cliente.ObterAsync<List<ClubeTurmaOpcaoApi>>(
            "api/turmas/clubes-gerenciaveis", ct);
        if (ca != HttpStatusCode.OK || cc != HttpStatusCode.OK)
            ModelState.AddModelError(string.Empty,
                erroA ?? erroC ?? "Não foi possível consultar opções de turma.");
        modelo.Atividades = atividades ?? [];
        modelo.Clubes = clubes ?? [];
    }
}
