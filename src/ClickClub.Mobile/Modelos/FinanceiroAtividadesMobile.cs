using System.Globalization;

namespace ClickClub.Mobile.Modelos;

public static class FormatoMobile
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");
    public static string Moeda(decimal valor) => valor.ToString("C", Brasil);
}

public sealed record FaturaMobile(long Id, string Competencia, string Vencimento,
    string Status, decimal ValorTotal, decimal MultaPrevista)
{
    public string Resumo => $"{Competencia} · {Status}\nVencimento: {Vencimento}\nTotal: {FormatoMobile.Moeda(ValorTotal)}";
    public bool EmAberto => Status.Equals("EmAberto", StringComparison.OrdinalIgnoreCase);
}
public sealed record ItemFaturaMobile(string Descricao, decimal Valor)
{
    public string Resumo => $"{Descricao}: {FormatoMobile.Moeda(Valor)}";
}
public sealed record FaturaDetalheMobile(FaturaMobile Cabecalho,
    IReadOnlyList<ItemFaturaMobile> Itens);

public sealed record TurmaMobile(long Id, string Atividade, string Tipo, string Clube,
    string Inicio, string Fim, int Capacidade, string Status)
{
    public string Resumo => $"{Atividade} ({Tipo})\n{Clube}\n{Inicio} até {Fim}\nCapacidade: {Capacidade} · {Status}";
}
public sealed record VagasTurmaMobile(int Confirmadas, int EmEspera, int VagasDisponiveis)
{
    public string Resumo => $"Inscritos: {Confirmadas} · Espera: {EmEspera} · Vagas: {VagasDisponiveis}";
}
public sealed record EventoMobile(long Id, string Nome, string Tipo, string Clube,
    string Inicio, string Fim, string PublicoAlvo, string Status, int? Vagas)
{
    public string Resumo => $"{Nome} ({Tipo})\n{Clube}\n{Inicio} até {Fim}\nPúblico: {PublicoAlvo} · {Status}"
        + (Vagas is null ? "" : $"\nVagas: {Vagas}");
}
public sealed record InscricaoMobile(long Id, long ReferenciaId, string Nome,
    string Clube, string Inicio, string Status, int? PosicaoEspera, bool EhTurma)
{
    public string Resumo => $"{Nome} · {Clube}\nInício: {Inicio}\nSituação: {Status}"
        + (PosicaoEspera is null ? "" : $" · Espera nº {PosicaoEspera}");
}

// Somente campos devolvidos pelo servidor: não calcular competência ou valor
// financeiro da transferência no Android.
public sealed record PreviaLocacaoMobile(long ReservaId, string Status,
    decimal Principal, decimal Acrescimo, string Competencia,
    string Vencimento, bool PossuiPreviaServidor)
{
    public string Resumo => $"Status: {Status}\nPrincipal: {FormatoMobile.Moeda(Principal)}"
        + $"\nAcréscimo informado pela API: {FormatoMobile.Moeda(Acrescimo)}"
        + $"\nCompetência: {Competencia}\nVencimento: {Vencimento}";
}
