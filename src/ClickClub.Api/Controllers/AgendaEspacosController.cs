using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/espacos/{espacoId:long}")]
[Authorize(Roles = "AdministradorGeral,FuncionarioUnidade,PresidenteClube")]
public sealed class AgendaEspacosController(AgendaEspacoRepositorio repositorio)
    : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    private bool AdminGeral => User.IsInRole("AdministradorGeral");

    [HttpGet("horarios")]
    public async Task<IActionResult> Horarios(long espacoId, CancellationToken ct)
    {
        var (resultado, itens) = await repositorio.ListarHorariosAsync(
            espacoId, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoAgenda.Concluido
            ? Ok(itens) : Erro(resultado);
    }

    [HttpPost("horarios")]
    public async Task<IActionResult> CriarHorario(long espacoId,
        [FromBody] HorarioEspacoEntrada entrada, CancellationToken ct)
    {
        var resultado = await repositorio.CriarHorarioAsync(
            espacoId, entrada, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoAgenda.Concluido
            ? StatusCode(StatusCodes.Status201Created) : Erro(resultado);
    }

    [HttpPatch("horarios/{horarioId:long}/situacao")]
    public async Task<IActionResult> AlterarHorario(long espacoId,
        long horarioId, [FromBody] SituacaoAgendaEntrada entrada,
        CancellationToken ct)
    {
        var resultado = await repositorio.AlterarHorarioAsync(
            espacoId, horarioId, entrada.Ativo, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoAgenda.Concluido
            ? NoContent() : Erro(resultado);
    }

    [HttpGet("manutencoes")]
    public async Task<IActionResult> Manutencoes(long espacoId,
        CancellationToken ct)
    {
        var (resultado, itens) = await repositorio.ListarManutencoesAsync(
            espacoId, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoAgenda.Concluido
            ? Ok(itens) : Erro(resultado);
    }

    [HttpGet("ocupacoes")]
    public async Task<IActionResult> Ocupacoes(
        long espacoId, [FromQuery] int ano, [FromQuery] int mes,
        CancellationToken ct)
    {
        var (resultado, itens) = await repositorio.ListarOcupacoesAsync(
            espacoId, ano, mes, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoAgenda.Concluido
            ? Ok(itens) : Erro(resultado);
    }

    [HttpPost("manutencoes")]
    public async Task<IActionResult> CriarManutencao(long espacoId,
        [FromBody] ManutencaoEspacoEntrada entrada, CancellationToken ct)
    {
        var resultado = await repositorio.CriarManutencaoAsync(
            espacoId, entrada, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoAgenda.Concluido
            ? StatusCode(StatusCodes.Status201Created) : Erro(resultado);
    }

    [HttpPatch("manutencoes/{manutencaoId:long}/situacao")]
    public async Task<IActionResult> AlterarManutencao(long espacoId,
        long manutencaoId, [FromBody] SituacaoAgendaEntrada entrada,
        CancellationToken ct)
    {
        var resultado = await repositorio.AlterarManutencaoAsync(
            espacoId, manutencaoId, entrada.Ativo, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoAgenda.Concluido
            ? NoContent() : Erro(resultado);
    }

    private IActionResult Erro(ResultadoAgenda resultado) => resultado switch
    {
        ResultadoAgenda.NaoEncontrado => NotFound(),
        ResultadoAgenda.ForaDoEscopo => Forbid(),
        ResultadoAgenda.ConflitoHorario =>
            Conflict(new { erro = "O horário se sobrepõe a outro período ativo." }),
        ResultadoAgenda.ConflitoReserva =>
            Conflict(new { erro =
                "Existe uma reserva confirmada nesse intervalo. Não é permitido bloqueá-la por manutenção." }),
        ResultadoAgenda.FusoInvalido =>
            Conflict(new { erro = "Fuso horário do clube não está configurado corretamente." }),
        ResultadoAgenda.PeriodoInvalido =>
            BadRequest(new { erro =
                "Período inválido. Verifique as horas e as datas locais do clube." }),
        _ => BadRequest(new { erro = "Operação inválida." })
    };
}
