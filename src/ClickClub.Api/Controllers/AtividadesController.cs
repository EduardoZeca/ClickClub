using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/atividades")]
[Authorize]
public sealed class AtividadesController(AtividadeRepositorio repositorio) : ControllerBase
{
    private long UsuarioId => long.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await repositorio.ListarAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Consultar(long id, CancellationToken ct)
    {
        var item = await repositorio.BuscarAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "AdministradorGeral")]
    public async Task<IActionResult> Criar([FromBody] AtividadeEntrada entrada,
        CancellationToken ct)
    {
        if (entrada.Nome.Trim().Length < 2 || entrada.Tipo.Trim().Length < 2)
            return BadRequest(new { erro = "Nome e tipo devem ter ao menos 2 caracteres úteis." });
        try
        {
            var id = await repositorio.CriarAsync(entrada, UsuarioId, ct);
            return CreatedAtAction(nameof(Consultar), new { id },
                await repositorio.BuscarAsync(id, ct));
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Já existe atividade com este nome e tipo." });
        }
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "AdministradorGeral")]
    public async Task<IActionResult> Atualizar(long id,
        [FromBody] AtividadeEntrada entrada, CancellationToken ct)
    {
        if (entrada.Nome.Trim().Length < 2 || entrada.Tipo.Trim().Length < 2)
            return BadRequest(new { erro = "Nome e tipo devem ter ao menos 2 caracteres úteis." });
        try
        {
            return await repositorio.AtualizarAsync(id, entrada, UsuarioId, ct)
                ? NoContent() : NotFound();
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Já existe atividade com este nome e tipo." });
        }
    }
}
