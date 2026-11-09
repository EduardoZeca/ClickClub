using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using ClickClub.Api.Validacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/adesoes")]
[AllowAnonymous]
public sealed class AdesoesController(AdesaoRepositorio repositorio) : ControllerBase
{
    [HttpGet("opcoes")]
    public async Task<IActionResult> Opcoes(CancellationToken ct) =>
        Ok(await repositorio.OpcoesPublicasAsync(ct));

    [HttpPost("solicitar")]
    [EnableRateLimiting("AdesaoPublica")]
    public async Task<IActionResult> Solicitar(
        [FromBody] AdesaoSolicitacaoEntrada entrada, CancellationToken ct)
    {
        if (!CpfValidador.Valido(entrada.Cpf))
            return BadRequest(new { erro = "CPF inválido." });

        if (entrada.DataNascimento.HasValue &&
            (entrada.DataNascimento.Value.Year < 1900 ||
             entrada.DataNascimento.Value.Date > DateTime.UtcNow.Date))
            return BadRequest(new { erro = "Data de nascimento inválida." });

        if (entrada.Interesses is null || entrada.Interesses.Count > 20 ||
            entrada.Interesses.Any(x => x is null || x.Trim().Length > 100))
            return BadRequest(new { erro = "Informe até 20 interesses de no máximo 100 caracteres." });

        try
        {
            var criada = await repositorio.CriarAsync(entrada, ct);
            // A resposta contém o código privado somente nesta criação.
            Response.Headers.CacheControl = "no-store";
            return StatusCode(StatusCodes.Status201Created, criada);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro =
                "Não foi possível cadastrar: CPF ou solicitação já registrado." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpPost("consultar")]
    [EnableRateLimiting("AdesaoPublica")]
    public async Task<IActionResult> Consultar(
        [FromBody] AdesaoConsultaEntrada entrada, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var estado = await repositorio.ConsultarPorCodigoAsync(entrada.Codigo, ct);
        return estado is null ? NotFound(new { erro = "Código não encontrado." }) : Ok(estado);
    }
}
