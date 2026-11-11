using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/financeiro")]
[Authorize(Roles = "AdministradorGeral,Titular,Tesoureiro")]
public sealed class FinanceiroController(
    PoliticaFinanceiraRepositorio politicas,
    FaturaRepositorio faturas,
    QuitacaoMensalidadeRepositorio quitacoes,
    FinanceiroLocacoesRepositorio locacoes) : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet("opcoes")]
    [Authorize(Policy = "SomenteAdministradorGeral")]
    public async Task<IActionResult> Opcoes(CancellationToken ct) =>
        Ok(await politicas.OpcoesAsync(ct));

    [HttpGet("politicas")]
    [Authorize(Policy = "SomenteAdministradorGeral")]
    public async Task<IActionResult> ListarPoliticas(CancellationToken ct) =>
        Ok(await politicas.ListarAsync(ct));

    [HttpPost("politicas")]
    [Authorize(Policy = "SomenteAdministradorGeral")]
    public async Task<IActionResult> CriarPolitica(
        [FromBody] PoliticaFinanceiraEntrada entrada, CancellationToken ct)
    {
        try
        {
            var id = await politicas.CriarAsync(entrada, UsuarioId, ct);
            return Ok(new { id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Já existe uma política para essa vigência, clube e categoria." });
        }
    }

    [HttpPost("faturas/emitir")]
    [Authorize(Policy = "SomenteAdministradorGeral")]
    public async Task<IActionResult> Emitir(
        [FromBody] EmissaoMensalidadeEntrada entrada, CancellationToken ct)
    {
        try
        {
            return Ok(await faturas.EmitirAsync(entrada, UsuarioId, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Fatura já existente para esta associação e competência." });
        }
    }

    [HttpGet("faturas")]
    [Authorize(Roles = "AdministradorGeral,Tesoureiro")]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(User.IsInRole("AdministradorGeral")
            ? await faturas.ListarAdministracaoAsync(ct)
            : await faturas.ListarTesoureiroAsync(UsuarioId, ct));

    [HttpGet("locacoes")]
    [Authorize(Roles = "AdministradorGeral,Tesoureiro")]
    public async Task<IActionResult> ListarLocacoes(CancellationToken ct) =>
        Ok(await locacoes.ListarAsync(
            UsuarioId, User.IsInRole("AdministradorGeral"), ct));

    [HttpGet("faturas/minhas")]
    [Authorize(Roles = "Titular")]
    public async Task<IActionResult> Minhas(CancellationToken ct) =>
        Ok(await faturas.ListarTitularAsync(UsuarioId, ct));

    [HttpGet("faturas/{id:long}")]
    public async Task<IActionResult> Detalhar(long id, CancellationToken ct)
    {
        var fatura = await faturas.BuscarAsync(
            id, UsuarioId, User.IsInRole("AdministradorGeral"),
            User.IsInRole("Tesoureiro"), ct);
        return fatura is null ? NotFound() : Ok(fatura);
    }

    [HttpPost("faturas/{id:long}/quitar")]
    public async Task<IActionResult> Quitar(long id, CancellationToken ct)
    {
        try
        {
            var resultado = await quitacoes.QuitarAsync(
                id, UsuarioId, User.IsInRole("AdministradorGeral"),
                User.IsInRole("Tesoureiro"), ct);
            return resultado is null ? NotFound() : Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
    }
}
