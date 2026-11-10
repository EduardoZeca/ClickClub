using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed record EspacoReservaOpcaoApi(
    long Id, string Clube, string Nome, string Tipo,
    int Capacidade, decimal TaxaBase, string FusoHorario);

public sealed record ReservaApi(
    long Id, string Clube, string Espaco, string Tipo,
    DateTime InicioLocal, DateTime FimLocal,
    string? Finalidade, int QuantidadeParticipantes, decimal ValorContratado,
    string Status, string SituacaoTaxa, string Responsavel);

public sealed record CancelamentoReservaApi(
    long ReservaId, decimal MultaSimulada,
    decimal CreditoSimuladoPendente, string SituacaoFinanceira);

public sealed record PermissaoReservaDependenteApi(
    long VinculoId, string Nome, DateTime? DataNascimento,
    bool PrecisaAutorizacao, bool PermitirReservaPaga);

public sealed class ReservaFormulario
{
    [Range(1, long.MaxValue, ErrorMessage = "Selecione o espaço.")]
    public long EspacoId { get; set; }
    [Required(ErrorMessage = "Informe o início da utilização.")]
    public DateTime? InicioLocal { get; set; }
    [Required(ErrorMessage = "Informe o fim da utilização.")]
    public DateTime? FimLocal { get; set; }
    [Range(1, 10000, ErrorMessage = "Informe entre 1 e 10.000 participantes.")]
    public int QuantidadeParticipantes { get; set; } = 1;
    [StringLength(250)] public string? Finalidade { get; set; }
    [StringLength(8000)] public string? ConvidadosTexto { get; set; }
    public bool ListaConvidadosCompleta { get; set; }
    public IReadOnlyList<EspacoReservaOpcaoApi> Espacos { get; set; } = [];
}

public sealed record ConvidadoReservaApi(
    long Id, string Nome, string IdentificacaoMinima);

public sealed record ConvidadosReservaApi(
    IReadOnlyList<ConvidadoReservaApi> Itens,
    DateTime LimiteEdicaoUtc, bool PodeAlterar);

public sealed class ConvidadosReservaFormulario
{
    [Range(1, long.MaxValue)] public long ReservaId { get; set; }
    [StringLength(8000)] public string? ConvidadosTexto { get; set; }
    public ReservaApi? Reserva { get; set; }
    public ConvidadosReservaApi? EstadoConvidados { get; set; }
}

public sealed record ConvidadoReservaEnvio(string Nome,
    string IdentificacaoMinima);

public static class ConversorConvidados
{
    public static bool TentarLer(string? linhas,
        out List<ConvidadoReservaEnvio> convidados)
    {
        convidados = [];
        if (string.IsNullOrWhiteSpace(linhas)) return true;

        foreach (var linha in linhas.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.IsNullOrWhiteSpace(linha)) continue;
            var texto = linha.Trim();
            var corte = texto.IndexOf('|');
            var tamanhoSeparador = 1;
            if (corte < 0)
            {
                corte = texto.IndexOf(" - ", StringComparison.Ordinal);
                if (corte >= 0) tamanhoSeparador = 3;
                else corte = texto.LastIndexOf('-');
            }
            if (corte < 0) return false;

            var nome = texto[..corte].Trim();
            var identificacao = texto[(corte + tamanhoSeparador)..].Trim();
            if (nome.Length is < 2 or > 150 ||
                identificacao.Length is < 3 or > 60)
                return false;

            convidados.Add(new ConvidadoReservaEnvio(
                nome, identificacao));
        }

        return convidados.Count <= 10000;
    }

    public static string ParaTexto(IEnumerable<ConvidadoReservaApi> convidados) =>
        string.Join(Environment.NewLine,
            convidados.Select(c => $"{c.Nome} | {c.IdentificacaoMinima}"));
}
