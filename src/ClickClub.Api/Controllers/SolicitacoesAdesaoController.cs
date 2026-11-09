using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/solicitacoes-adesao")]
[Authorize(Roles = "AdministradorGeral,PresidenteClube")]
public sealed class SolicitacoesAdesaoController(AdesaoRepositorio repositorio)
    : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    private bool AdminGeral => User.IsInRole("AdministradorGeral");

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await repositorio.ListarAsync(UsuarioId, AdminGeral, ct));

    [HttpGet("categorias")]
    public async Task<IActionResult> Categorias(CancellationToken ct) =>
        Ok(await repositorio.CategoriasAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken ct)
    {
        var detalhe = await repositorio.BuscarAsync(id, UsuarioId, AdminGeral, ct);
        // A consulta não revela se um protocolo de outro clube existe.
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [HttpPost("{id:long}/decisao")]
    public async Task<IActionResult> Decidir(
        long id, [FromBody] AdesaoDecisaoEntrada entrada, CancellationToken ct)
    {
        if (!entrada.Aprovar && string.IsNullOrWhiteSpace(entrada.Motivo))
            return BadRequest(new { erro = "Informe o motivo da rejeição." });

        try
        {
            var resultado = await repositorio.DecidirAsync(
                id, entrada, UsuarioId, AdminGeral, ct);
            return resultado switch
            {
                ResultadoDecisaoAdesao.Concluida => NoContent(),
                ResultadoDecisaoAdesao.NaoEncontrada => NotFound(),
                ResultadoDecisaoAdesao.ForaDoEscopo => Forbid(),
                ResultadoDecisaoAdesao.JaAnalisada =>
                    Conflict(new { erro = "A solicitação já foi analisada." }),
                _ => BadRequest(new { erro =
                    "Dados inválidos: verifique categoria, matrícula e situação do clube." })
            };
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro =
                "Matrícula já cadastrada ou associação ativa existente." });
        }
    }
}
