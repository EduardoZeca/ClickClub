using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ouvidoria")]
public sealed class OuvidoriaController(OuvidoriaRepositorio repositorio) : ControllerBase
{
    private long UsuarioId => long.TryParse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : 0;

    private bool Admin => User.IsInRole("AdministradorGeral");

    [HttpGet("minhas")]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Minhas(CancellationToken ct) =>
        Ok(await repositorio.MinhasAsync(UsuarioId, ct));

    [HttpGet("clubes")]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Clubes(CancellationToken ct) =>
        Ok(await repositorio.ClubesAtivosAsync(ct));

    [HttpGet("gerenciar")]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Gerenciar(CancellationToken ct) =>
        Ok(await repositorio.GerenciaveisAsync(UsuarioId, Admin, ct));

    [HttpGet("{id:long}")]
    [Authorize(Roles = "Titular,Dependente,AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Detalhe(long id, CancellationToken ct)
    {
        var item = await repositorio.DetalheAsync(id, UsuarioId, Admin, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Criar(
        [FromBody] ChamadoOuvidoriaEntrada entrada, CancellationToken ct)
    {
        var (resultado, id) = await repositorio.CriarAsync(entrada, UsuarioId, ct);
        return resultado == ResultadoOuvidoria.Concluido
            ? CreatedAtAction(nameof(Detalhe), new { id },
                await repositorio.DetalheAsync(id, UsuarioId, Admin, ct))
            : RespostaErro(resultado);
    }

    [HttpPost("{id:long}/respostas")]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Responder(long id,
        [FromBody] RespostaOuvidoriaEntrada entrada, CancellationToken ct)
    {
        var resultado = await repositorio.ResponderAsync(
            id, entrada, UsuarioId, Admin, ct);
        return resultado == ResultadoOuvidoria.Concluido
            ? NoContent() : RespostaErro(resultado);
    }

    [HttpPost("{id:long}/encerrar")]
    [Authorize(Roles = "AdministradorGeral,PresidenteClube,FuncionarioUnidade")]
    public async Task<IActionResult> Encerrar(long id, CancellationToken ct)
    {
        var resultado = await repositorio.EncerrarAsync(id, UsuarioId, Admin, ct);
        return resultado == ResultadoOuvidoria.Concluido
            ? NoContent() : RespostaErro(resultado);
    }

    private IActionResult RespostaErro(ResultadoOuvidoria resultado) =>
        resultado switch
        {
            ResultadoOuvidoria.NaoEncontrado => NotFound(),
            ResultadoOuvidoria.ForaDoEscopo => Forbid(),
            ResultadoOuvidoria.UnidadeInativa => Conflict(new
                { erro = "Clube indisponível para abertura de chamado." }),
            ResultadoOuvidoria.SemContaAtiva => Conflict(new
                { erro = "Usuário ou pessoa sem cadastro ativo." }),
            ResultadoOuvidoria.JaEncerrado => Conflict(new
                { erro = "O chamado já está encerrado." }),
            ResultadoOuvidoria.SemResposta => Conflict(new
                { erro = "Registre uma resposta antes de encerrar o chamado." }),
            _ => BadRequest(new
                { erro = "Informe tipo, categoria, assunto e descrição válidos." })
        };
}
