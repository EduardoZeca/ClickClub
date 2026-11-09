using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/equipe")]
[Authorize(Policy = "SomenteAdministradorGeral")]
public sealed class EquipeController(EquipeRepositorio repositorio)
    : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

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
    public async Task<IActionResult> Criar(
        [FromBody] EquipeCriacaoEntrada entrada, CancellationToken ct)
    {
        if (!EquipeRepositorio.PapelLocalValido(entrada.Papel))
            return BadRequest(new { erro = "Perfil de equipe inválido." });
        if (entrada.Login.Trim().Contains(' '))
            return BadRequest(new { erro = "Login não pode conter espaços." });

        try
        {
            var id = await repositorio.CriarAsync(entrada, UsuarioId, ct);
            return CreatedAtAction(nameof(Consultar), new { id },
                await repositorio.BuscarAsync(id, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Login já cadastrado." });
        }
    }

    [HttpPost("{id:long}/atribuicoes")]
    public async Task<IActionResult> Atribuir(
        long id, [FromBody] EquipeAtribuicaoEntrada entrada, CancellationToken ct)
    {
        if (!EquipeRepositorio.PapelLocalValido(entrada.Papel))
            return BadRequest(new { erro = "Perfil de equipe inválido." });

        try
        {
            var criado = await repositorio.AtribuirAsync(id, entrada, UsuarioId, ct);
            return criado ? NoContent() : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Este papel já foi atribuído neste clube." });
        }
    }

    [HttpDelete("{id:long}/atribuicoes")]
    public async Task<IActionResult> Revogar(
        long id, [FromBody] EquipeAtribuicaoEntrada entrada, CancellationToken ct)
    {
        if (!EquipeRepositorio.PapelLocalValido(entrada.Papel))
            return BadRequest(new { erro = "Perfil de equipe inválido." });

        try
        {
            var removido = await repositorio.RevogarAsync(
                id, entrada, UsuarioId, ct);
            return removido ? NoContent() : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpPatch("{id:long}/situacao")]
    public async Task<IActionResult> AlterarSituacao(
        long id, [FromBody] EquipeSituacaoEntrada entrada, CancellationToken ct)
    {
        if (id == UsuarioId)
            return Conflict(new { erro = "Não é possível desativar a própria conta." });
        var alterado = await repositorio.AlterarSituacaoAsync(
            id, entrada.Ativo, UsuarioId, ct);
        return alterado ? NoContent() : NotFound();
    }

    [HttpPut("{id:long}/senha")]
    public async Task<IActionResult> RedefinirSenha(
        long id, [FromBody] EquipeSenhaEntrada entrada, CancellationToken ct)
    {
        var alterado = await repositorio.RedefinirSenhaAsync(
            id, entrada.NovaSenha, UsuarioId, ct);
        return alterado ? NoContent() : NotFound();
    }
}
