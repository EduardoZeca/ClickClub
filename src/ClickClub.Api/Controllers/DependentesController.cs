using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using ClickClub.Api.Validacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/dependentes")]
[Authorize(Policy = "SomenteAdministradorGeral")]
public sealed class DependentesController(DependenteRepositorio repositorio)
    : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] long associacaoId, CancellationToken ct)
    {
        if (associacaoId <= 0) return BadRequest();
        return Ok(await repositorio.ListarAsync(associacaoId, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Consultar(long id, CancellationToken ct)
    {
        var dependente = await repositorio.BuscarAsync(id, ct);
        return dependente is null ? NotFound() : Ok(dependente);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] DependenteEntrada entrada, CancellationToken ct)
    {
        var erro = Validar(entrada);
        if (erro is not null) return BadRequest(new { erro });

        try
        {
            var id = await repositorio.CriarAsync(entrada, UsuarioId, ct);
            return CreatedAtAction(nameof(Consultar), new { id },
                await repositorio.BuscarAsync(id, ct));
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "CPF ou vínculo já cadastrado." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(
        long id, [FromBody] DependenteEntrada entrada, CancellationToken ct)
    {
        var erro = Validar(entrada);
        if (erro is not null) return BadRequest(new { erro });

        try
        {
            return await repositorio.AtualizarAsync(id, entrada, UsuarioId, ct)
                ? NoContent() : NotFound();
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "CPF ou vínculo já cadastrado." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpPatch("{id:long}/situacao")]
    public async Task<IActionResult> AlterarSituacao(
        long id, [FromBody] SituacaoDependenteEntrada entrada,
        CancellationToken ct)
    {
        try
        {
            var alterado = await repositorio.AlterarSituacaoAsync(
                id, entrada.Ativo, UsuarioId, ct);
            return alterado ? NoContent() : NotFound();
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Dependente já está ativo em outra associação." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
    }

    private static string? Validar(DependenteEntrada entrada)
    {
        if (!string.IsNullOrWhiteSpace(entrada.Cpf) &&
            !CpfValidador.Valido(entrada.Cpf))
            return "CPF inválido: verifique os dígitos verificadores.";

        if (entrada.DataNascimento.Date > DateTime.Today ||
            entrada.DataNascimento.Year < 1900)
            return "Informe uma data de nascimento válida.";

        return null;
    }
}
