using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Api.Controllers;

[ApiController]
[Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade,Tesoureiro")]
[Route("api/dashboard")]
public sealed class DashboardController(DashboardRepositorio repositorio) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obter(
        [FromQuery] DateOnly? inicio, [FromQuery] DateOnly? fim,
        [FromQuery] long? unidadeId, CancellationToken ct)
    {
        var agora = DateOnly.FromDateTime(DateTime.UtcNow);
        var dataInicio = inicio ?? new DateOnly(agora.Year, agora.Month, 1);
        var dataFim = fim ?? agora;

        if (dataInicio > dataFim || dataFim == DateOnly.MaxValue ||
            unidadeId is <= 0)
            return BadRequest(new { erro = "Informe período válido e clube positivo." });

        if (!long.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                out var usuarioId) || usuarioId <= 0)
            return Unauthorized();

        var (foraDoEscopo, resposta) = await repositorio.ConsultarAsync(
            usuarioId, User.IsInRole("AdministradorGeral"), unidadeId,
            dataInicio, dataFim, ct);
        return foraDoEscopo ? Forbid() : Ok(resposta);
    }
}
