using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/avisos")]
public sealed class AvisosController(AvisoRepositorio repositorio) : ControllerBase
{
    private long UsuarioId => long.TryParse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : 0;

    private bool Admin => User.IsInRole("AdministradorGeral");

    [HttpGet]
    public async Task<IActionResult> Publicados(
        [FromQuery] long? unidadeId, CancellationToken ct)
    {
        if (unidadeId is <= 0)
            return BadRequest(new { erro = "Clube inválido." });
        return Ok(await repositorio.PublicadosAsync(unidadeId, ct));
    }

    [HttpGet("gerenciar")]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Gerenciar(CancellationToken ct) =>
        Ok(await repositorio.GerenciaveisAsync(UsuarioId, Admin, ct));

    [HttpGet("clubes-gerenciaveis")]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Clubes(CancellationToken ct) =>
        Ok(await repositorio.ClubesGerenciaveisAsync(UsuarioId, Admin, ct));

    [HttpGet("{id:long}")]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Consultar(long id, CancellationToken ct)
    {
        var (resultado, item) = await repositorio.BuscarAsync(
            id, UsuarioId, Admin, ct);
        return resultado == ResultadoAviso.Concluido
            ? Ok(item) : Erro(resultado);
    }

    [HttpPost]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Criar([FromBody] AvisoEntrada entrada,
        CancellationToken ct)
    {
        var (resultado, id) = await repositorio.CriarAsync(
            entrada, UsuarioId, Admin, ct);
        if (resultado != ResultadoAviso.Concluido) return Erro(resultado);
        var (_, criado) = await repositorio.BuscarAsync(id, UsuarioId, Admin, ct);
        return CreatedAtAction(nameof(Consultar), new { id }, criado);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Atualizar(long id,
        [FromBody] AvisoEntrada entrada, CancellationToken ct)
    {
        var resultado = await repositorio.AtualizarAsync(
            id, entrada, UsuarioId, Admin, ct);
        return resultado == ResultadoAviso.Concluido ? NoContent() : Erro(resultado);
    }

    [HttpPatch("{id:long}/situacao")]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> AlterarSituacao(long id,
        [FromBody] SituacaoAvisoEntrada entrada, CancellationToken ct)
    {
        var resultado = await repositorio.AlterarSituacaoAsync(
            id, entrada.Ativo, UsuarioId, Admin, ct);
        return resultado == ResultadoAviso.Concluido ? NoContent() : Erro(resultado);
    }

    private IActionResult Erro(ResultadoAviso resultado) => resultado switch
    {
        ResultadoAviso.NaoEncontrado => NotFound(),
        ResultadoAviso.ForaDoEscopo => Forbid(),
        ResultadoAviso.UnidadeInativa => Conflict(new
            { erro = "O clube está inativo e não aceita publicação." }),
        ResultadoAviso.UnidadeImutavel => BadRequest(new
            { erro = "Não é permitido transferir um aviso de clube nem torná-lo geral." }),
        ResultadoAviso.PeriodoInvalido => BadRequest(new
            { erro = "O início do aviso deve ser anterior ao fim." }),
        _ => BadRequest(new { erro = "Informe título, texto, período UTC e clube válidos." })
    };
}
