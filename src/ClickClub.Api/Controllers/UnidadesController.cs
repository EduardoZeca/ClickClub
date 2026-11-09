using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/unidades")]
[Authorize(Policy = "SomenteAdministradorGeral")]
public sealed class UnidadesController(UnidadeRepositorio repositorio) : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await repositorio.ListarAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Consultar(long id, CancellationToken ct)
    {
        var unidade = await repositorio.BuscarAsync(id, ct);
        return unidade is null ? NotFound() : Ok(unidade);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] UnidadeEntrada entrada, CancellationToken ct)
    {
        try
        {
            var id = await repositorio.CriarAsync(entrada, UsuarioId, ct);
            return CreatedAtAction(nameof(Consultar), new { id },
                await repositorio.BuscarAsync(id, ct));
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Já existe um clube com essa sigla." });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(
        long id, [FromBody] UnidadeEntrada entrada, CancellationToken ct)
    {
        try
        {
            var alterado = await repositorio.AtualizarAsync(
                id, entrada, UsuarioId, ct);
            return alterado ? NoContent() : NotFound();
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Já existe um clube com essa sigla." });
        }
    }

    [HttpPatch("{id:long}/situacao")]
    public async Task<IActionResult> AlterarSituacao(
        long id, [FromBody] SituacaoUnidadeEntrada entrada,
        CancellationToken ct)
    {
        var alterado = await repositorio.AlterarSituacaoAsync(
            id, entrada.Ativa, UsuarioId, ct);
        return alterado ? NoContent() : NotFound();
    }
}
