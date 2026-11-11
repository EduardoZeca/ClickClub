using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class ReservaEntrada
{
    [Range(1, long.MaxValue)] public long EspacoId { get; set; }
    [Required] public DateTime? InicioLocal { get; set; }
    [Required] public DateTime? FimLocal { get; set; }
    [Range(1, 10000)] public int QuantidadeParticipantes { get; set; }
    [StringLength(250)] public string? Finalidade { get; set; }
    public List<ConvidadoReservaEntrada> Convidados { get; set; } = [];
    public bool ListaConvidadosCompleta { get; set; }
}

public sealed class ConvidadoReservaEntrada
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;
    [Required, StringLength(60, MinimumLength = 3)]
    public string IdentificacaoMinima { get; set; } = string.Empty;
}

public sealed class AtualizacaoConvidadosEntrada
{
    public List<ConvidadoReservaEntrada> Convidados { get; set; } = [];
    public bool ListaConvidadosCompleta { get; set; }
}

public sealed record ConvidadoReservaResposta(long Id, string Nome,
    string IdentificacaoMinima);

public sealed record ConvidadosReservaResposta(
    IReadOnlyList<ConvidadoReservaResposta> Itens,
    DateTime LimiteEdicaoUtc, bool PodeAlterar);

public sealed record EspacoReservaOpcao(
    long Id, string Clube, string Nome, string Tipo,
    int Capacidade, decimal TaxaBase, string FusoHorario);

public sealed record ReservaResposta(
    long Id, string Clube, string Espaco, string Tipo,
    DateTime InicioLocal, DateTime FimLocal,
    string? Finalidade, int QuantidadeParticipantes, decimal ValorContratado,
    string Status, string SituacaoTaxa, string Responsavel);

public sealed record CancelamentoReservaResposta(
    long ReservaId, decimal MultaSimulada,
    decimal CreditoSimuladoPendente, string SituacaoFinanceira);

public sealed record PermissaoReservaDependenteResposta(
    long VinculoId, string Nome, DateTime? DataNascimento,
    bool PrecisaAutorizacao, bool PermitirReservaPaga);

public sealed class PermissaoReservaDependenteEntrada
{
    public bool PermitirReservaPaga { get; set; }
}

public enum ResultadoReserva
{
    Concluida, SemAssociacaoAtiva, EspacoIndisponivel,
    PeriodoInvalido, ForaDoHorario, Manutencao,
    ConflitoReserva, CotaEsgotada, CapacidadeExcedida,
    AntecedenciaInsuficiente, ListaConvidadosInvalida,
    ListaConvidadosIncompleta, FusoInvalido,
    PermissaoTitularNecessaria, MensalidadeInadimplente
}

public enum ResultadoConvidados
{
    Concluido, ReservaNaoEncontrada, ForaDoPrazo,
    TipoNaoPermiteConvidados, ListaInvalida
}

public enum ResultadoCancelamento
{
    Concluido, ReservaNaoEncontrada, JaCancelada,
    UtilizacaoIniciada
}
