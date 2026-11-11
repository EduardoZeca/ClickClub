using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class AtividadeEntrada
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Descricao { get; set; }

    [Required, StringLength(80, MinimumLength = 2)]
    public string Tipo { get; set; } = string.Empty;
}

public sealed record AtividadeItem(long Id, string Nome, string? Descricao, string Tipo);

public sealed class TurmaEntrada
{
    [Range(1, long.MaxValue)] public long AtividadeId { get; set; }
    [Range(1, long.MaxValue)] public long UnidadeId { get; set; }
    [Range(1, long.MaxValue)] public long? InstrutorPessoaId { get; set; }
    [Required] public DateTime? InicioLocal { get; set; }
    [Required] public DateTime? FimLocal { get; set; }
    [Range(1, 10000)] public int Capacidade { get; set; }
}

public sealed class TurmaSituacaoEntrada
{
    [Required, RegularExpression("^(Aberta|Encerrada|Cancelada)$")]
    public string Status { get; set; } = string.Empty;
}

public sealed record TurmaItem(
    long Id, long AtividadeId, string Atividade, string Tipo,
    long UnidadeId, string Clube, string FusoHorario,
    long? InstrutorPessoaId, string? Instrutor,
    DateTime InicioLocal, DateTime FimLocal, int Capacidade, string Status);

public sealed record ClubeTurmaOpcao(long Id, string Nome, string FusoHorario);

public enum ResultadoTurma
{
    Concluido, NaoEncontrada, ForaDoEscopo, UnidadeInativa,
    UnidadeImutavel, AtividadeNaoEncontrada, InstrutorInvalido,
    PeriodoInvalido, FusoInvalido,
    PeriodoBloqueadoPorInscricoes, CapacidadeInferiorInscritos
}
