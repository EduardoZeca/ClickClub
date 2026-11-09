using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/contas")]
[Authorize(Policy = "SomenteAdministradorGeral")]
public sealed class ContasController(ContaAdministrativaRepositorio repositorio)
    : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet("pessoa/{pessoaId:long}")]
    public async Task<IActionResult> BuscarPorPessoa(
        long pessoaId, CancellationToken ct)
    {
        var conta = await repositorio.BuscarPorPessoaAsync(pessoaId, ct);
        return conta is null ? NotFound() : Ok(conta);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] ContaCriacaoEntrada entrada, CancellationToken ct)
    {
        if (entrada.Papel is not ("Titular" or "Dependente"))
            return BadRequest(new { erro = "Perfil de acesso inválido." });

        if (entrada.Login.Trim().Contains(' '))
            return BadRequest(new { erro = "O login não pode conter espaços." });

        try
        {
            var id = await repositorio.CriarAsync(entrada, UsuarioId, ct);
            return Created($"api/contas/pessoa/{entrada.PessoaId}",
                new { usuarioId = id, pessoaId = entrada.PessoaId,
                    login = entrada.Login.Trim(), papel = entrada.Papel });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Pessoa ou login já possui conta cadastrada." });
        }
    }

    [HttpPatch("{id:long}/situacao")]
    public async Task<IActionResult> AlterarSituacao(
        long id, [FromBody] SituacaoContaEntrada entrada, CancellationToken ct)
    {
        if (id == UsuarioId)
            return Conflict(new { erro = "Não é possível desativar a própria conta." });

        try
        {
            var alterado = await repositorio.AlterarSituacaoAsync(
                id, entrada.Ativo, UsuarioId, ct);
            return alterado ? NoContent() : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
    }

    [HttpPut("{id:long}/senha")]
    public async Task<IActionResult> RedefinirSenha(
        long id, [FromBody] ContaSenhaEntrada entrada, CancellationToken ct)
    {
        var alterado = await repositorio.RedefinirSenhaAsync(
            id, entrada.NovaSenha, UsuarioId, ct);
        return alterado ? NoContent() : NotFound();
    }
}
