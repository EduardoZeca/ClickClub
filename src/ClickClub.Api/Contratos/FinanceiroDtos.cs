using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class PoliticaFinanceiraEntrada
{
    [Range(1, long.MaxValue)]
    public long UnidadeId { get; set; }

    [Range(1, long.MaxValue)]
    public long CategoriaId { get; set; }

    [Range(typeof(decimal), "0", "9999999999.99")]
    public decimal ValorMensal { get; set; }

    [Required]
    public DateTime? VigenciaInicio { get; set; }
}

public sealed record PoliticaFinanceiraResposta(
    long Id, long UnidadeId, string Unidade, long CategoriaId,
    string Categoria, decimal ValorMensal, DateTime VigenciaInicio,
    DateTime? VigenciaFim);

public sealed record UnidadeFinanceiraOpcao(long Id, string Nome);
public sealed record CategoriaFinanceiraOpcao(long Id, string Nome);

public sealed record AssociacaoFinanceiraOpcao(
    long Id, string Matricula, string Titular, string Clube,
    string Categoria, DateTime DataInicio, string Status);

public sealed record FinanceiroOpcoesResposta(
    IReadOnlyList<UnidadeFinanceiraOpcao> Unidades,
    IReadOnlyList<CategoriaFinanceiraOpcao> Categorias,
    IReadOnlyList<AssociacaoFinanceiraOpcao> Associacoes);

public sealed class EmissaoMensalidadeEntrada
{
    [Range(1, long.MaxValue)]
    public long AssociacaoId { get; set; }

    [Required]
    public DateTime? Competencia { get; set; }
}

public sealed record EmissaoMensalidadeResposta(long FaturaId, bool JaExistia);

public sealed record FaturaResumoResposta(
    long Id, long AssociacaoId, string Matricula, string Titular,
    string Clube, DateTime Competencia, DateTime DataVencimento,
    decimal ValorTotal, string Status, int DiasAtraso,
    bool BloqueioPorAtraso, decimal MultaPendente,
    decimal TotalAtualizado);

public sealed record ItemFaturaResposta(
    long Id, string Tipo, string Descricao, decimal Valor,
    long? ReservaId, string? ClubeOrigem);

public sealed record FaturaDetalheResposta(
    FaturaResumoResposta Fatura, IReadOnlyList<ItemFaturaResposta> Itens);

public sealed record QuitacaoMensalidadeResposta(long FaturaId,
    bool JaQuitada, decimal ValorQuitado, decimal MultaAplicada);

// Visão de prestação de serviço: não expõe titular, dependentes, nem
// identificador da fatura do clube de origem a Tesoureiro externo.
public sealed record LocacaoFinanceiraResposta(
    long ReservaId, long UnidadePrestadoraId, string ClubePrestador,
    string ClubeOrigem, string Espaco, DateTime InicioLocal,
    decimal Principal, decimal Acrescimo, decimal Total,
    string SituacaoReserva, string SituacaoCobranca, string Modalidade,
    DateTime? CompetenciaDestino);
