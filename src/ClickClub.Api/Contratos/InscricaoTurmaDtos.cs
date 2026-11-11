namespace ClickClub.Api.Contratos;

public sealed record InscricaoTurmaCriada(
    long InscricaoId, long TurmaId, string Status, int? PosicaoEspera);

public sealed record InscricaoTurmaDetalhe(
    long Id, long TurmaId, long PessoaId, string NomePessoa,
    string Status, DateTime InscritaEmUtc, int? PosicaoEspera);

public sealed record MinhaInscricaoTurmaItem(
    long Id, long TurmaId, string Atividade, string Clube,
    DateTime InicioLocal, DateTime FimLocal,
    string Status, int? PosicaoEspera);

public sealed record VagasTurmaResposta(
    long TurmaId, int Capacidade, int Confirmadas,
    int EmEspera, int VagasDisponiveis);

public sealed record PromocaoTurmaResposta(
    long InscricaoId, long TurmaId, long PessoaId, string NomePessoa);

public enum ResultadoInscricaoTurma
{
    Concluida, TurmaNaoEncontrada, ForaDoEscopo,
    TurmaIndisponivel, SemAssociacaoAtiva, MensalidadeInadimplente,
    ConflitoHorario, InscricaoDuplicada, InscricaoNaoEncontrada,
    InscricaoJaCancelada, SemVagas, SemCandidatoElegivel, FusoInvalido
}
