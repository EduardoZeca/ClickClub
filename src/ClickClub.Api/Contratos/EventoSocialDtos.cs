using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class EventoSocialEntrada
{
    [Range(1, long.MaxValue)] public long UnidadeId { get; set; }
    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;
    [StringLength(2000)] public string? Descricao { get; set; }
    [Required] public DateTime? InicioLocal { get; set; }
    [Required] public DateTime? FimLocal { get; set; }
    [Required, RegularExpression("^(SomenteAssociados|PublicoGeral)$")]
    public string PublicoAlvo { get; set; } = "SomenteAssociados";
    [Required, RegularExpression("^(Social|Torneio)$")]
    public string Tipo { get; set; } = "Social";
    [Range(1, 10000)] public int? Capacidade { get; set; }
}

public sealed class SituacaoEventoEntrada
{
    [Required, RegularExpression("^(Publicado|Encerrado|Cancelado)$")]
    public string Status { get; set; } = string.Empty;
}

public sealed record EventoSocialItem(
    long Id, long UnidadeId, string Clube, string FusoHorario,
    string Nome, string? Descricao, DateTime InicioLocal, DateTime FimLocal,
    string PublicoAlvo, string Tipo, int? Capacidade, string Status,
    int Inscritos, int? VagasDisponiveis);

public sealed record InscricaoEventoItem(
    long Id, long EventoId, long PessoaId, string NomePessoa,
    string Status, DateTime InscritaEmUtc);

public sealed record MinhaInscricaoEventoItem(
    long Id, long EventoId, string Evento, string Clube,
    DateTime InicioLocal, string Status);

public sealed record InscricaoEventoCriada(long InscricaoId, long EventoId, string Status);

public enum ResultadoEvento
{
    Concluido, NaoEncontrado, ForaDoEscopo, UnidadeInativa,
    UnidadeImutavel, PeriodoInvalido, FusoInvalido,
    CapacidadeInvalida, DadosBloqueadosPorInscricoes,
    CapacidadeInferiorInscritos, Indisponivel,
    SemVagas, InscricaoDuplicada, SemAssociacaoAtiva,
    MensalidadeInadimplente, InscricaoNaoEncontrada, JaCancelada
}
