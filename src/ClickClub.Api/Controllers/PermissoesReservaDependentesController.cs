using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/reservas/permissoes-dependentes")]
[Authorize(Roles = "Titular")]
public sealed class PermissoesReservaDependentesController(
    PermissaoReservaDependenteRepositorio repositorio) : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await repositorio.ListarAsync(UsuarioId, ct));

    [HttpPut("{vinculoId:long}")]
    public async Task<IActionResult> Alterar(long vinculoId,
        [FromBody] PermissaoReservaDependenteEntrada entrada, CancellationToken ct)
    {
        var alterado = await repositorio.AlterarAsync(
            vinculoId, UsuarioId, entrada.PermitirReservaPaga, ct);
        return alterado ? NoContent() : NotFound();
    }
}
