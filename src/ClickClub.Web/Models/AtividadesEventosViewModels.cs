using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed record AtividadeApi(long Id, string Nome,
    string? Descricao, string Tipo);

public sealed class AtividadeFormulario
{
    public long Id { get; set; }
    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;
    [StringLength(1000)] public string? Descricao { get; set; }
    [Required, StringLength(80, MinimumLength = 2)]
    public string Tipo { get; set; } = string.Empty;

    public static AtividadeFormulario De(AtividadeApi item) => new()
    { Id = item.Id, Nome = item.Nome, Descricao = item.Descricao, Tipo = item.Tipo };
}

public sealed record ClubeTurmaOpcaoApi(long Id, string Nome, string FusoHorario);

public sealed record TurmaApi(
    long Id, long AtividadeId, string Atividade, string Tipo,
    long UnidadeId, string Clube, string FusoHorario,
    long? InstrutorPessoaId, string? Instrutor,
    DateTime InicioLocal, DateTime FimLocal, int Capacidade, string Status);

public sealed record VagasTurmaApi(long TurmaId, int Capacidade,
    int Confirmadas, int EmEspera, int VagasDisponiveis);

public sealed record MinhaInscricaoTurmaApi(
    long Id, long TurmaId, string Atividade, string Clube,
    DateTime InicioLocal, DateTime FimLocal,
    string Status, int? PosicaoEspera);

public sealed record InscricaoTurmaDetalheApi(
    long Id, long TurmaId, long PessoaId, string NomePessoa,
    string Status, DateTime InscritaEmUtc, int? PosicaoEspera);

public sealed record TurmaLinhaWeb(TurmaApi Turma, VagasTurmaApi? Vagas);

public sealed record TurmaGestaoWeb(TurmaApi Turma,
    IReadOnlyList<InscricaoTurmaDetalheApi> Inscricoes, VagasTurmaApi? Vagas);

public sealed class TurmaFormulario
{
    public long Id { get; set; }
    [Range(1, long.MaxValue)] public long AtividadeId { get; set; }
    [Range(1, long.MaxValue)] public long UnidadeId { get; set; }
    [Range(1, long.MaxValue)] public long? InstrutorPessoaId { get; set; }
    [Required] public DateTime? InicioLocal { get; set; }
    [Required] public DateTime? FimLocal { get; set; }
    [Range(1, 10000)] public int Capacidade { get; set; } = 15;
    public IReadOnlyList<AtividadeApi> Atividades { get; set; } = [];
    public IReadOnlyList<ClubeTurmaOpcaoApi> Clubes { get; set; } = [];

    public static TurmaFormulario De(TurmaApi item) => new()
    {
        Id = item.Id, AtividadeId = item.AtividadeId,
        UnidadeId = item.UnidadeId, InstrutorPessoaId = item.InstrutorPessoaId,
        InicioLocal = item.InicioLocal, FimLocal = item.FimLocal,
        Capacidade = item.Capacidade
    };
}

public sealed record EventoSocialApi(
    long Id, long UnidadeId, string Clube, string FusoHorario,
    string Nome, string? Descricao, DateTime InicioLocal, DateTime FimLocal,
    string PublicoAlvo, string Tipo, int? Capacidade, string Status,
    int Inscritos, int? VagasDisponiveis);

public sealed record MinhaInscricaoEventoApi(
    long Id, long EventoId, string Evento, string Clube,
    DateTime InicioLocal, string Status);

public sealed record InscricaoEventoApi(
    long Id, long EventoId, long PessoaId, string NomePessoa,
    string Status, DateTime InscritaEmUtc);

public sealed record VisitanteEventoApi(
    long Id, long EventoId, string Nome,
    string IdentificacaoMinima, string Situacao);

public sealed record EventoInscritosWeb(EventoSocialApi Evento,
    IReadOnlyList<InscricaoEventoApi> Inscricoes);

public sealed record VisitantesEventoWeb(EventoSocialApi Evento,
    IReadOnlyList<VisitanteEventoApi> Visitantes);

public sealed class EventoFormulario
{
    public long Id { get; set; }
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
    public IReadOnlyList<ClubeTurmaOpcaoApi> Clubes { get; set; } = [];

    public static EventoFormulario De(EventoSocialApi item) => new()
    {
        Id = item.Id, UnidadeId = item.UnidadeId,
        Nome = item.Nome, Descricao = item.Descricao,
        InicioLocal = item.InicioLocal, FimLocal = item.FimLocal,
        PublicoAlvo = item.PublicoAlvo, Tipo = item.Tipo,
        Capacidade = item.Capacidade
    };
}

public sealed class VisitanteEventoFormulario
{
    [Range(1, long.MaxValue)] public long EventoId { get; set; }
    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;
    [Required, StringLength(60, MinimumLength = 3)]
    public string IdentificacaoMinima { get; set; } = string.Empty;
}
