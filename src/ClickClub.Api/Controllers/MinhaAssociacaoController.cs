using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/minha-associacao")]
[Authorize(Roles = "Titular,Dependente")]
public sealed class MinhaAssociacaoController(
    MinhaAssociacaoRepositorio repositorio) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Consultar(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            out var usuarioId))
            return Unauthorized();

        var minha = await repositorio.ConsultarAsync(
            usuarioId, User.IsInRole("Titular"), ct);
        return minha is null ? NotFound() : Ok(minha);
    }
}
