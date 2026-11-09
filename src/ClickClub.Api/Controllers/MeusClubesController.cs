using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/meus-clubes")]
[Authorize(Roles =
    "AdministradorGeral,FuncionarioUnidade,Porteiro,PresidenteClube,Tesoureiro")]
public sealed class MeusClubesController(EquipeRepositorio repositorio)
    : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var clubes = await repositorio.ListarClubesAsync(
            UsuarioId, User.IsInRole("AdministradorGeral"), ct);
        return Ok(clubes);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Consultar(long id, CancellationToken ct)
    {
        var clubes = await repositorio.ListarClubesAsync(
            UsuarioId, User.IsInRole("AdministradorGeral"), ct);
        var clube = clubes.FirstOrDefault(x => x.Id == id);
        return clube is null ? Forbid() : Ok(clube);
    }
}
