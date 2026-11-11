using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Contratos;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/reservas")]
[Authorize(Roles = "Titular,Dependente")]
public sealed class ReservasController(
    ReservaRepositorio repositorio,
    ConvidadosReservaRepositorio convidadosRepositorio,
    CancelamentoReservaRepositorio cancelamentoRepositorio,
    CobrancaLocacaoRepositorio cobrancas) : ControllerBase
{
    private long UsuarioId => long.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    private bool Titular => User.IsInRole("Titular");

    [HttpGet("espacos")]
    public async Task<IActionResult> Espacos(CancellationToken ct) =>
        Ok(await repositorio.OpcoesAsync(ct));

    [HttpGet("minhas")]
    public async Task<IActionResult> Minhas(CancellationToken ct) =>
        Ok(await repositorio.MinhasAsync(UsuarioId, Titular, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Consultar(long id, CancellationToken ct)
    {
        var reserva = await repositorio.BuscarMinhaAsync(id, UsuarioId, Titular, ct);
        return reserva is null ? NotFound() : Ok(reserva);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] ReservaEntrada entrada, CancellationToken ct)
    {
        var (resultado, id) = await repositorio.CriarAsync(entrada, UsuarioId, Titular, ct);
        if (resultado != ResultadoReserva.Concluida)
            return Erro(resultado);
        return CreatedAtAction(nameof(Consultar), new { id },
            await repositorio.BuscarMinhaAsync(id, UsuarioId, Titular, ct));
    }

    [HttpGet("{id:long}/convidados")]
    public async Task<IActionResult> Convidados(long id, CancellationToken ct)
    {
        var convidados = await convidadosRepositorio.ConsultarAsync(
            id, UsuarioId, Titular, ct);
        return convidados is null ? NotFound() : Ok(convidados);
    }

    [HttpPut("{id:long}/convidados")]
    public async Task<IActionResult> AtualizarConvidados(
        long id, [FromBody] AtualizacaoConvidadosEntrada entrada,
        CancellationToken ct)
    {
        var resultado = await convidadosRepositorio.AtualizarAsync(
            id, UsuarioId, Titular, entrada, ct);
        return resultado switch
        {
            ResultadoConvidados.Concluido => NoContent(),
            ResultadoConvidados.ReservaNaoEncontrada => NotFound(),
            ResultadoConvidados.TipoNaoPermiteConvidados =>
                BadRequest(new { erro = "Este tipo de espaço não permite lista de convidados." }),
            ResultadoConvidados.ForaDoPrazo =>
                Conflict(new { erro = "Alterações de convidados são bloqueadas nas 24 horas anteriores ao início." }),
            ResultadoConvidados.ListaInvalida =>
                BadRequest(new { erro = "Lista de convidados inválida ou acima da capacidade informada." }),
            _ => BadRequest(new { erro = "Operação inválida." })
        };
    }

    [HttpPost("{id:long}/cancelamento")]
    public async Task<IActionResult> Cancelar(long id, CancellationToken ct)
    {
        var (resultado, dados) = await cancelamentoRepositorio.CancelarAsync(
            id, UsuarioId, Titular, ct);
        return resultado switch
        {
            ResultadoCancelamento.Concluido => Ok(dados),
            ResultadoCancelamento.ReservaNaoEncontrada => NotFound(),
            ResultadoCancelamento.JaCancelada => Conflict(new
            {
                erro = "Reserva já cancelada. Nenhum novo acerto foi gerado."
            }),
            ResultadoCancelamento.UtilizacaoIniciada => Conflict(new
            {
                erro = "Não é possível cancelar uma reserva cujo uso já começou."
            }),
            _ => BadRequest(new { erro = "Cancelamento não permitido." })
        };
    }

    [HttpGet("{id:long}/cobranca")]
    [Authorize(Roles = "Titular")]
    public async Task<IActionResult> ConsultarCobranca(long id, CancellationToken ct)
    {
        try
        {
            var dados = await cobrancas.ConsultarAsync(id, UsuarioId, ct);
            return dados is null ? NotFound() : Ok(dados);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
    }

    [HttpPost("{id:long}/cobranca/quitar-avulsa")]
    [Authorize(Roles = "Titular")]
    public async Task<IActionResult> QuitarTaxa(long id, CancellationToken ct) =>
        await DecidirCobranca(id, false, null, ct);

    [HttpPost("{id:long}/cobranca/transferir")]
    [Authorize(Roles = "Titular")]
    public async Task<IActionResult> TransferirTaxa(long id,
        [FromBody] TransferenciaLocacaoEntrada entrada, CancellationToken ct) =>
        await DecidirCobranca(id, true, entrada.CompetenciaConfirmada, ct);

    private async Task<IActionResult> DecidirCobranca(
        long id, bool transferir, DateTime? competenciaConfirmada,
        CancellationToken ct)
    {
        try
        {
            var resultado = await cobrancas.DecidirAsync(
                id, UsuarioId, transferir, competenciaConfirmada, ct);
            return resultado is null ? NotFound() : Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { erro =
                "Cobrança ou item já registrado. Recarregue a reserva antes de repetir a operação." });
        }
    }

    private IActionResult Erro(ResultadoReserva resultado) => resultado switch
    {
        ResultadoReserva.SemAssociacaoAtiva =>
            Forbid(),
        ResultadoReserva.PermissaoTitularNecessaria =>
            Conflict(new { erro =
                "Para reservar espaço pago, o dependente menor de idade precisa da permissão geral do titular. Solicite a habilitação em Reservas > Permissões de dependentes." }),
        ResultadoReserva.MensalidadeInadimplente =>
            Conflict(new { erro =
                "A associação possui mensalidade vencida há mais de 10 dias. A quitação simulada da mensalidade deve ser registrada pelo titular ou pela administração antes de confirmar novas reservas." }),
        ResultadoReserva.EspacoIndisponivel =>
            NotFound(new { erro = "Espaço não encontrado ou inativo." }),
        ResultadoReserva.AntecedenciaInsuficiente =>
            Conflict(new { erro =
                "Salões exigem pelo menos 24 horas exatas de antecedência." }),
        ResultadoReserva.ListaConvidadosInvalida =>
            BadRequest(new { erro =
                "Lista de convidados inválida ou superior à capacidade informada." }),
        ResultadoReserva.ListaConvidadosIncompleta =>
            BadRequest(new { erro =
                "Nas últimas 24 horas, confirme que a lista inicial de convidados está completa." }),
        ResultadoReserva.PeriodoInvalido =>
            BadRequest(new { erro = "Informe um período futuro válido no horário local do clube." }),
        ResultadoReserva.ForaDoHorario =>
            Conflict(new { erro = "O intervalo não está inteiramente dentro do funcionamento do espaço." }),
        ResultadoReserva.Manutencao =>
            Conflict(new { erro = "O espaço está bloqueado para manutenção nesse intervalo." }),
        ResultadoReserva.ConflitoReserva =>
            Conflict(new { erro = "Já existe uma reserva confirmada nesse intervalo." }),
        ResultadoReserva.CotaEsgotada =>
            Conflict(new { erro = "Limite de três reservas confirmadas no mês de início atingido." }),
        ResultadoReserva.CapacidadeExcedida =>
            BadRequest(new { erro = "Quantidade de participantes inválida ou acima da capacidade do espaço." }),
        ResultadoReserva.FusoInvalido =>
            Conflict(new { erro = "Fuso horário inválido no cadastro do clube." }),
        _ => BadRequest(new { erro = "Operação inválida." })
    };
}
