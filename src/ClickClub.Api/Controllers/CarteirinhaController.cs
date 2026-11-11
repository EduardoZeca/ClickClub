using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/carteirinha")]
[Authorize(Roles = "Titular,Dependente")]
public sealed class CarteirinhaController(
    CarteirinhaRepositorio repositorio) : ControllerBase
{
    private long? UsuarioId => long.TryParse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : null;

    [HttpGet("minha")]
    public async Task<IActionResult> Consultar(CancellationToken ct)
    {
        if (UsuarioId is not long id)
            return Unauthorized();

        try
        {
            var credencial = await repositorio.ConsultarMinhaAsync(id, ct);
            return credencial is null
                ? NotFound(new { erro = "Nenhuma carteirinha válida disponível para esta associação." })
                : Ok(credencial);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
    }

    [HttpPost("minha/renovar")]
    public async Task<IActionResult> EmitirOuRenovar(CancellationToken ct)
    {
        if (UsuarioId is not long id)
            return Unauthorized();

        try
        {
            var credencial = await repositorio.EmitirOuRenovarMinhaAsync(id, ct);
            return credencial is null
                ? NotFound(new { erro = "É necessário possuir associação ativa para emitir carteirinha." })
                : Ok(credencial);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
    }
}
