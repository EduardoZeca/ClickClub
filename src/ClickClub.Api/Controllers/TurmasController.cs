using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/turmas")]
[Authorize]
public sealed class TurmasController(TurmaRepositorio repositorio) : ControllerBase
{
    private long UsuarioId => long.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    private bool AdminGeral => User.IsInRole("AdministradorGeral");

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? unidadeId,
        CancellationToken ct)
    {
        if (unidadeId.HasValue && unidadeId.Value <= 0)
            return BadRequest(new { erro = "Clube inválido." });
        return Ok(await repositorio.ListarAsync(unidadeId, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Consultar(long id, CancellationToken ct)
    {
        var item = await repositorio.BuscarAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("clubes-gerenciaveis")]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> ClubesGerenciaveis(CancellationToken ct) =>
        Ok(await repositorio.ClubesGerenciaveisAsync(UsuarioId, AdminGeral, ct));

    [HttpPost]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Criar([FromBody] TurmaEntrada entrada,
        CancellationToken ct)
    {
        var (resultado, id) = await repositorio.CriarAsync(
            entrada, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoTurma.Concluido
            ? CreatedAtAction(nameof(Consultar), new { id },
                await repositorio.BuscarAsync(id, ct))
            : RespostaErro(resultado);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Atualizar(long id,
        [FromBody] TurmaEntrada entrada, CancellationToken ct)
    {
        var resultado = await repositorio.AtualizarAsync(
            id, entrada, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoTurma.Concluido
            ? NoContent() : RespostaErro(resultado);
    }

    [HttpPatch("{id:long}/situacao")]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> AlterarSituacao(long id,
        [FromBody] TurmaSituacaoEntrada entrada, CancellationToken ct)
    {
        var resultado = await repositorio.AlterarSituacaoAsync(
            id, entrada.Status, UsuarioId, AdminGeral, ct);
        return resultado == ResultadoTurma.Concluido
            ? NoContent() : RespostaErro(resultado);
    }

    private IActionResult RespostaErro(ResultadoTurma resultado) => resultado switch
    {
        ResultadoTurma.NaoEncontrada => NotFound(),
        ResultadoTurma.ForaDoEscopo => Forbid(),
        ResultadoTurma.UnidadeInativa => Conflict(new
            { erro = "O clube selecionado está inativo." }),
        ResultadoTurma.UnidadeImutavel => BadRequest(new
            { erro = "Não é possível transferir turma entre clubes." }),
        ResultadoTurma.AtividadeNaoEncontrada => BadRequest(new
            { erro = "Atividade não encontrada no catálogo." }),
        ResultadoTurma.InstrutorInvalido => BadRequest(new
            { erro = "Instrutor não encontrado ou inativo." }),
        ResultadoTurma.PeriodoInvalido => BadRequest(new
            { erro = "Período inválido: informe início anterior ao fim e datas sem fuso UTC." }),
        ResultadoTurma.FusoInvalido => BadRequest(new
            { erro = "Fuso do clube não reconhecido ou horário local ambíguo/inexistente." }),
        ResultadoTurma.PeriodoBloqueadoPorInscricoes => Conflict(new
            { erro = "Turma com inscrições ativas não pode alterar atividade ou período." }),
        ResultadoTurma.CapacidadeInferiorInscritos => Conflict(new
            { erro = "Capacidade não pode ser menor que o número de confirmados." }),
        _ => BadRequest(new { erro = "Operação inválida." })
    };
}
