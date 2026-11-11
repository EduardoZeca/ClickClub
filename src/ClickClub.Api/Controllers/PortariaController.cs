using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/portaria")]
[Authorize(Roles = "Porteiro,AdministradorGeral")]
public sealed class PortariaController(
    PortariaRepositorio repositorio,
    PortariaEventoRepositorio visitantesEventos) : ControllerBase
{
    private long? OperadorId => long.TryParse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : null;

    private bool Administrador => User.IsInRole("AdministradorGeral");

    [HttpGet("unidades")]
    public async Task<IActionResult> Unidades(CancellationToken ct)
    {
        if (OperadorId is not long id)
            return Unauthorized();

        return Ok(await repositorio.UnidadesAsync(id, Administrador, ct));
    }

    [HttpPost("validar-qr")]
    public async Task<IActionResult> Validar(
        [FromBody] ValidacaoQrEntrada entrada, CancellationToken ct)
    {
        if (OperadorId is not long id)
            return Unauthorized();

        if (entrada.UnidadeId <= 0 ||
            entrada.CodigoQr is { Length: > 1024 } ||
            entrada.TipoAcesso is not ("EntradaGeral" or "UsoReserva") ||
            (entrada.TipoAcesso == "UsoReserva" && entrada.ReservaId is not > 0) ||
            (entrada.TipoAcesso == "EntradaGeral" && entrada.ReservaId is not null))
            return BadRequest(new { erro =
                "Informe uma unidade válida, um tipo de acesso e, para uso de espaço, a reserva correspondente." });

        // Resultado Negado é HTTP 200 com decisão de negócio auditada.
        // Fora de escopo é HTTP 403; falta da API nunca libera o acesso.
        var resultado = await repositorio.ValidarAsync(
            entrada, id, Administrador, ct);
        return resultado is null ? Forbid() : Ok(resultado);
    }

    [HttpGet("registros")]
    public async Task<IActionResult> Registros(
        [FromQuery] long unidadeId, [FromQuery] int limite = 50,
        CancellationToken ct = default)
    {
        if (OperadorId is not long id)
            return Unauthorized();

        if (unidadeId <= 0 || limite is < 1 or > 100)
            return BadRequest(new { erro = "Unidade e limite (1 a 100) inválidos." });

        var lista = await repositorio.HistoricoAsync(
            unidadeId, id, Administrador, limite, ct);
        return lista is null ? Forbid() : Ok(lista);
    }

    [HttpPost("validar-convidado")]
    public async Task<IActionResult> ValidarConvidado(
        [FromBody] ValidacaoConvidadoEntrada entrada, CancellationToken ct)
    {
        if (OperadorId is not long id)
            return Unauthorized();

        if (entrada.UnidadeId <= 0 || entrada.ReservaId <= 0 ||
            string.IsNullOrWhiteSpace(entrada.IdentificacaoMinima) ||
            entrada.IdentificacaoMinima.Trim().Length is < 3 or > 60)
            return BadRequest(new { erro =
                "Informe unidade, reserva e identificação do convidado válida." });

        var resultado = await repositorio.ValidarConvidadoAsync(
            entrada, id, Administrador, ct);
        return resultado is null ? Forbid() : Ok(resultado);
    }

    [HttpPost("validar-visitante-evento")]
    public async Task<IActionResult> ValidarVisitanteEvento(
        [FromBody] ValidacaoVisitanteEventoEntrada entrada,
        CancellationToken ct)
    {
        if (OperadorId is not long id) return Unauthorized();
        if (entrada.UnidadeId <= 0 || entrada.EventoId <= 0 ||
            string.IsNullOrWhiteSpace(entrada.IdentificacaoMinima) ||
            entrada.IdentificacaoMinima.Trim().Length is < 3 or > 60)
            return BadRequest(new { erro =
                "Informe clube, evento e identificação cadastrada válidos." });
        var resultado = await visitantesEventos.ValidarAsync(
            entrada, id, Administrador, ct);
        return resultado is null ? Forbid() : Ok(resultado);
    }
}
