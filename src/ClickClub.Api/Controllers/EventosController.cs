using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/eventos")]
public sealed class EventosController(
    EventoSocialRepositorio eventos, InscricaoEventoRepositorio inscricoes,
    VisitanteEventoRepositorio visitantes)
    : ControllerBase
{
    private long UsuarioId => long.TryParse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : 0;
    private bool Admin => User.IsInRole("AdministradorGeral");
    private bool Associado => User.IsInRole("Titular") || User.IsInRole("Dependente");

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Listar([FromQuery] long? unidadeId,
        CancellationToken ct)
    {
        if (unidadeId.HasValue && unidadeId <= 0)
            return BadRequest(new { erro = "Clube inválido." });
        return Ok(await eventos.PublicadosAsync(Associado, unidadeId, ct));
    }

    [HttpGet("{id:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> Consultar(long id, CancellationToken ct)
    {
        var item = await eventos.ConsultarAsync(id, UsuarioId, Admin, Associado, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("gerenciar")]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Gerenciar(CancellationToken ct) =>
        Ok(await eventos.GerenciaveisAsync(UsuarioId, Admin, ct));

    [HttpPost]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Criar(
        [FromBody] EventoSocialEntrada entrada, CancellationToken ct)
    {
        var (resultado, id) = await eventos.CriarAsync(
            entrada, UsuarioId, Admin, ct);
        return resultado == ResultadoEvento.Concluido
            ? CreatedAtAction(nameof(Consultar), new { id },
                await eventos.ConsultarAsync(id, UsuarioId, Admin, Associado, ct))
            : RespostaErro(resultado);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Atualizar(long id,
        [FromBody] EventoSocialEntrada entrada, CancellationToken ct)
    {
        var resultado = await eventos.AtualizarAsync(
            id, entrada, UsuarioId, Admin, ct);
        return resultado == ResultadoEvento.Concluido
            ? NoContent() : RespostaErro(resultado);
    }

    [HttpPatch("{id:long}/situacao")]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> AlterarSituacao(long id,
        [FromBody] SituacaoEventoEntrada entrada, CancellationToken ct)
    {
        var resultado = await eventos.SituacaoAsync(
            id, entrada.Status, UsuarioId, Admin, ct);
        return resultado == ResultadoEvento.Concluido
            ? NoContent() : RespostaErro(resultado);
    }

    [HttpGet("minhas-inscricoes")]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Minhas(CancellationToken ct) =>
        Ok(await inscricoes.MinhasAsync(UsuarioId, ct));

    [HttpPost("{id:long}/inscricoes")]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> Inscrever(long id, CancellationToken ct)
    {
        try
        {
            var (resultado, item) = await inscricoes.InscreverMinhaAsync(id, UsuarioId, ct);
            return resultado == ResultadoEvento.Concluido
                ? StatusCode(StatusCodes.Status201Created, item)
                : RespostaErro(resultado);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Inscrição ativa duplicada no evento." });
        }
    }

    [HttpDelete("{id:long}/inscricoes/minha")]
    [Authorize(Roles = "Titular,Dependente")]
    public async Task<IActionResult> CancelarMinha(long id, CancellationToken ct)
    {
        var resultado = await inscricoes.CancelarMinhaAsync(id, UsuarioId, ct);
        return resultado == ResultadoEvento.Concluido
            ? NoContent() : RespostaErro(resultado);
    }

    [HttpGet("{id:long}/inscricoes")]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Inscritos(long id, CancellationToken ct)
    {
        var (resultado, itens) = await inscricoes.GestaoAsync(id, UsuarioId, Admin, ct);
        return resultado == ResultadoEvento.Concluido
            ? Ok(itens) : RespostaErro(resultado);
    }

    [HttpGet("{id:long}/visitantes")]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> Visitantes(long id, CancellationToken ct)
    {
        var (resultado, itens) = await visitantes.ListarAsync(
            id, UsuarioId, Admin, ct);
        return resultado == ResultadoVisitanteEvento.Concluido
            ? Ok(itens) : RespostaVisitanteErro(resultado);
    }

    [HttpPost("{id:long}/visitantes")]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> CadastrarVisitante(long id,
        [FromBody] VisitanteEventoEntrada entrada, CancellationToken ct)
    {
        if (entrada.Nome.Trim().Length < 2 ||
            entrada.IdentificacaoMinima.Trim().Length < 3)
            return BadRequest(new { erro = "Nome ou identificação incompletos." });
        try
        {
            var (resultado, visitanteId) = await visitantes.CriarAsync(
                id, entrada, UsuarioId, Admin, ct);
            return resultado == ResultadoVisitanteEvento.Concluido
                ? StatusCode(StatusCodes.Status201Created,
                    new { id = visitanteId, eventoId = id })
                : RespostaVisitanteErro(resultado);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro = "Identificação já cadastrada no evento." });
        }
    }

    [HttpPatch("{id:long}/visitantes/{visitanteId:long}/situacao")]
    [Authorize(Roles = "AdministradorGeral,FuncionarioUnidade")]
    public async Task<IActionResult> SituacaoVisitante(long id, long visitanteId,
        [FromBody] SituacaoVisitanteEventoEntrada entrada, CancellationToken ct)
    {
        var resultado = await visitantes.AlterarSituacaoAsync(
            id, visitanteId, entrada.Situacao, UsuarioId, Admin, ct);
        return resultado == ResultadoVisitanteEvento.Concluido
            ? NoContent() : RespostaVisitanteErro(resultado);
    }

    private IActionResult RespostaVisitanteErro(ResultadoVisitanteEvento resultado) =>
        resultado switch
        {
            ResultadoVisitanteEvento.EventoNaoEncontrado or
                ResultadoVisitanteEvento.VisitanteNaoEncontrado => NotFound(),
            ResultadoVisitanteEvento.ForaDoEscopo => Forbid(),
            ResultadoVisitanteEvento.PublicoNaoPermitido => Conflict(new
                { erro = "Visitantes externos só são cadastrados em eventos públicos." }),
            ResultadoVisitanteEvento.EventoIndisponivel => Conflict(new
                { erro = "O evento não aceita novos visitantes." }),
            ResultadoVisitanteEvento.CapacidadeEsgotada => Conflict(new
                { erro = "Todas as vagas do evento estão ocupadas." }),
            ResultadoVisitanteEvento.Duplicado => Conflict(new
                { erro = "Identificação de visitante duplicada." }),
            _ => BadRequest(new { erro = "Operação de visitante inválida." })
        };

    private IActionResult RespostaErro(ResultadoEvento resultado) => resultado switch
    {
        ResultadoEvento.NaoEncontrado or ResultadoEvento.InscricaoNaoEncontrada
            => NotFound(),
        ResultadoEvento.ForaDoEscopo => Forbid(),
        ResultadoEvento.UnidadeInativa => Conflict(new
            { erro = "Clube selecionado está inativo." }),
        ResultadoEvento.UnidadeImutavel => BadRequest(new
            { erro = "Não é permitido transferir o evento para outro clube." }),
        ResultadoEvento.PeriodoInvalido => BadRequest(new
            { erro = "O início deve ser anterior ao fim e informar hora local sem offset." }),
        ResultadoEvento.FusoInvalido => BadRequest(new
            { erro = "Fuso do clube não reconhecido ou horário ambíguo/inexistente." }),
        ResultadoEvento.CapacidadeInvalida => BadRequest(new
            { erro = "Torneio exige capacidade positiva e nome válido." }),
        ResultadoEvento.DadosBloqueadosPorInscricoes => Conflict(new
            { erro = "Evento com inscritos não pode mudar período, tipo ou público-alvo." }),
        ResultadoEvento.CapacidadeInferiorInscritos => Conflict(new
            { erro = "Capacidade inferior ao número de inscritos existentes." }),
        ResultadoEvento.InscricaoDuplicada => Conflict(new
            { erro = "Você já está inscrito neste evento." }),
        ResultadoEvento.SemVagas => Conflict(new
            { erro = "Não há vagas disponíveis neste evento." }),
        ResultadoEvento.Indisponivel => Conflict(new
            { erro = "Evento não recebe inscrições ou já começou." }),
        ResultadoEvento.SemAssociacaoAtiva => Conflict(new
            { erro = "Associação ativa não encontrada." }),
        ResultadoEvento.MensalidadeInadimplente => Conflict(new
            { erro = "Mensalidade com mais de dez dias de atraso." }),
        ResultadoEvento.JaCancelada => Conflict(new
            { erro = "A inscrição já foi cancelada." }),
        _ => BadRequest(new { erro = "Operação inválida." })
    };
}
