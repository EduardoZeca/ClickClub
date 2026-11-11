using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/categorias")]
[Authorize(Policy = "SomenteAdministradorGeral")]
public sealed class CategoriasController(CategoriaRepositorio repositorio) : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await repositorio.ListarAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Consultar(long id, CancellationToken ct)
    {
        var categoria = await repositorio.BuscarAsync(id, ct);
        return categoria is null ? NotFound() : Ok(categoria);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] CategoriaEntrada entrada, CancellationToken ct)
    {
        try
        {
            var id = await repositorio.CriarAsync(entrada.Nome, UsuarioId, ct);
            return CreatedAtAction(nameof(Consultar), new { id },
                await repositorio.BuscarAsync(id, ct));
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Já existe uma categoria com esse nome." });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(
        long id, [FromBody] CategoriaEntrada entrada, CancellationToken ct)
    {
        try
        {
            var alterado = await repositorio.RenomearAsync(
                id, entrada.Nome, UsuarioId, ct);
            return alterado ? NoContent() : NotFound();
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Já existe uma categoria com esse nome." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
    }

    [HttpPatch("{id:long}/situacao")]
    public async Task<IActionResult> AlterarSituacao(
        long id, [FromBody] SituacaoCategoriaEntrada entrada,
        CancellationToken ct)
    {
        var alterado = await repositorio.AlterarSituacaoAsync(
            id, entrada.Ativa, UsuarioId, ct);
        return alterado ? NoContent() : NotFound();
    }
}
