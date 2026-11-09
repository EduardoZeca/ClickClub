using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using ClickClub.Api.Validacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/titulares")]
[Authorize(Policy = "SomenteAdministradorGeral")]
public sealed class TitularesController(TitularRepositorio repositorio) : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await repositorio.ListarAsync(ct));

    [HttpGet("opcoes")]
    public async Task<IActionResult> Opcoes(CancellationToken ct) =>
        Ok(await repositorio.ListarOpcoesAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Consultar(long id, CancellationToken ct)
    {
        var titular = await repositorio.BuscarAsync(id, ct);
        return titular is null ? NotFound() : Ok(titular);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] TitularEntrada entrada, CancellationToken ct)
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
            return Conflict(new { erro =
                "CPF, matrícula ou vínculo ativo já cadastrado." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(
        long id, [FromBody] TitularEntrada entrada, CancellationToken ct)
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
            return Conflict(new { erro =
                "CPF, matrícula ou vínculo ativo já cadastrado." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpPatch("{id:long}/situacao")]
    public async Task<IActionResult> AlterarSituacao(
        long id, [FromBody] TitularSituacaoEntrada entrada, CancellationToken ct)
    {
        if (entrada.Status is not ("Ativa" or "Suspensa"))
            return BadRequest(new { erro = "Situação inválida." });

        try
        {
            var alterado = await repositorio.AlterarSituacaoAsync(
                id, entrada.Status == "Ativa", UsuarioId, ct);
            return alterado ? NoContent() : NotFound();
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Já existe outra associação ativa para o titular." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
    }

    private static string? Validar(TitularEntrada entrada)
    {
        if (!CpfValidador.Valido(entrada.Cpf))
            return "CPF inválido: verifique os dígitos verificadores.";

        if (entrada.DataInicio.Year < 1900 || entrada.DataInicio.Year > 2100)
            return "Informe uma data de início válida.";

        if (entrada.DataNascimento.HasValue &&
            (entrada.DataNascimento.Value.Date > DateTime.Today ||
             entrada.DataNascimento.Value.Year < 1900))
            return "Data de nascimento inválida.";

        if (entrada.Interesses is null ||
            entrada.Interesses.Any(x => x is null || x.Trim().Length > 100))
            return "Cada interesse deve ter no máximo 100 caracteres.";

        return null;
    }
}
