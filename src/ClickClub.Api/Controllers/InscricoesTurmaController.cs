using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/turmas")]
[Authorize]
public sealed class InscricoesTurmaController(InscricaoTurmaRepositorio repositorio)
    : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    private bool AdminGeral => User.IsInRole("AdministradorGeral");

    [HttpGet("minhas-inscricoes")]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Minhas(CancellationToken ct) =>
        Ok(await repositorio.MinhasAsync(UsuarioId, ct));

    [HttpGet("{turmaId:long}/vagas")]
    public async Task<IActionResult> Vagas(long turmaId, CancellationToken ct)
    {
        var vagas = await repositorio.VagasAsync(turmaId, ct);
        return vagas is null ? NotFound() : Ok(vagas);
    }

    [HttpPost("{turmaId:long}/inscricoes")]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Inscrever(long turmaId, CancellationToken ct)
    {
        try
        {
            var (resultado, inscricao) = await repositorio.InscreverMinhaAsync(
                turmaId, UsuarioId, ct);
            return resultado == ResultadoInscricaoTurma.Concluida
                ? StatusCode(StatusCodes.Status201Created, inscricao)
                : RespostaErro(resultado);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Inscrição ativa duplicada." });
        }
    }

    [HttpDelete("{turmaId:long}/inscricoes/minha")]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> CancelarMinha(long turmaId, CancellationToken ct)
    {
        var resultado = await repositorio.CancelarMinhaAsync(turmaId, UsuarioId, ct);
        return resultado == ResultadoInscricaoTurma.Concluida
            ? NoContent() : RespostaErro(resultado);
    }

    [HttpGet("{turmaId:long}/inscricoes")]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Inscricoes(long turmaId, CancellationToken ct)
    {
        var (resultado, itens) = await repositorio.ListarGestaoAsync(
            turmaId, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoInscricaoTurma.Concluida
            ? Ok(itens) : RespostaErro(resultado);
    }

    [HttpPost("{turmaId:long}/promover-proximo")]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Promover(long turmaId, CancellationToken ct)
    {
        var (resultado, promovida) = await repositorio.PromoverProximoAsync(
            turmaId, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoInscricaoTurma.Concluida
            ? Ok(promovida) : RespostaErro(resultado);
    }

    private IActionResult RespostaErro(ResultadoInscricaoTurma resultado) =>
        resultado switch
        {
            ResultadoInscricaoTurma.TurmaNaoEncontrada or
                ResultadoInscricaoTurma.InscricaoNaoEncontrada => NotFound(),
            ResultadoInscricaoTurma.ForaDoEscopo => Forbid(),
            ResultadoInscricaoTurma.InscricaoDuplicada => Conflict(new
                { erro = "Você já possui inscrição ativa nesta turma." }),
            ResultadoInscricaoTurma.ConflitoHorario => Conflict(new
                { erro = "Você já está inscrito em outra turma com horário sobreposto." }),
            ResultadoInscricaoTurma.InscricaoJaCancelada => Conflict(new
                { erro = "Esta inscrição já foi cancelada." }),
            ResultadoInscricaoTurma.SemVagas => Conflict(new
                { erro = "Não há vagas disponíveis para promoção." }),
            ResultadoInscricaoTurma.SemCandidatoElegivel => Conflict(new
                { erro = "Não há candidatos elegíveis na lista de espera." }),
            ResultadoInscricaoTurma.SemAssociacaoAtiva => Conflict(new
                { erro = "Associação ativa ou vínculo elegível não encontrado." }),
            ResultadoInscricaoTurma.MensalidadeInadimplente => Conflict(new
                { erro = "Mensalidade vencida há mais de dez dias." }),
            ResultadoInscricaoTurma.TurmaIndisponivel => Conflict(new
                { erro = "Turma encerrada, cancelada, iniciada ou com clube inativo." }),
            ResultadoInscricaoTurma.FusoInvalido => Conflict(new
                { erro = "Fuso horário do clube de origem inválido." }),
            _ => BadRequest(new { erro = "Operação de inscrição inválida." })
        };
}
