using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/espacos")]
[Authorize(Roles = "AdministradorGeral,FuncionarioUnidade,PresidenteClube")]
public sealed class EspacosController(EspacoRepositorio repositorio) : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    private bool AdminGeral => User.IsInRole("AdministradorGeral");

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await repositorio.ListarAsync(UsuarioId, AdminGeral, ct));

    [HttpGet("clubes")]
    public async Task<IActionResult> Clubes(CancellationToken ct) =>
        Ok(await repositorio.OpcoesClubesAsync(UsuarioId, AdminGeral, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Consultar(long id, CancellationToken ct)
    {
        var espaco = await repositorio.BuscarAsync(id, UsuarioId, AdminGeral, ct);
        return espaco is null ? NotFound() : Ok(espaco);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] EspacoEntrada entrada, CancellationToken ct)
    {
        try
        {
            var (resultado, id) = await repositorio.CriarAsync(
                entrada, UsuarioId, AdminGeral, ct);
            if (resultado != ResultadoEspaco.Concluido)
                return RespostaErro(resultado);
            return CreatedAtAction(nameof(Consultar), new { id },
                await repositorio.BuscarAsync(id, UsuarioId, AdminGeral, ct));
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Já existe um espaço com esse nome no clube." });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(long id,
        [FromBody] EspacoEntrada entrada, CancellationToken ct)
    {
        try
        {
            var resultado = await repositorio.AtualizarAsync(
                id, entrada, UsuarioId, AdminGeral, ct);
            return resultado == ResultadoEspaco.Concluido
                ? NoContent() : RespostaErro(resultado);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Já existe um espaço com esse nome no clube." });
        }
    }

    [HttpPatch("{id:long}/situacao")]
    public async Task<IActionResult> AlterarSituacao(long id,
        [FromBody] EspacoSituacaoEntrada entrada, CancellationToken ct)
    {
        var resultado = await repositorio.AlterarSituacaoAsync(
            id, entrada.Ativo, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoEspaco.Concluido
            ? NoContent() : RespostaErro(resultado);
    }

    private IActionResult RespostaErro(ResultadoEspaco resultado) => resultado switch
    {
        ResultadoEspaco.NaoEncontrado => NotFound(),
        ResultadoEspaco.ForaDoEscopo => Forbid(),
        ResultadoEspaco.UnidadeInativa =>
            Conflict(new { erro = "O clube selecionado está inativo." }),
        ResultadoEspaco.UnidadeNaoPodeSerAlterada =>
            BadRequest(new { erro =
                "O espaço não pode ser transferido de clube. Desative-o e cadastre outro." }),
        _ => BadRequest(new { erro = "Operação inválida." })
    };
}
