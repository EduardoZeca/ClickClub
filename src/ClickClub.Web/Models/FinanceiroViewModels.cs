using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed record UnidadeFinanceiraApi(long Id, string Nome);
public sealed record CategoriaFinanceiraApi(long Id, string Nome);
public sealed record AssociacaoFinanceiraApi(long Id, string Matricula,
    string Titular, string Clube, string Categoria, DateTime DataInicio,
    string Status);
public sealed record OpcoesFinanceirasApi(
    IReadOnlyList<UnidadeFinanceiraApi> Unidades,
    IReadOnlyList<CategoriaFinanceiraApi> Categorias,
    IReadOnlyList<AssociacaoFinanceiraApi> Associacoes);

public sealed record PoliticaFinanceiraApi(long Id, long UnidadeId,
    string Unidade, long CategoriaId, string Categoria,
    decimal ValorMensal, DateTime VigenciaInicio, DateTime? VigenciaFim);

public sealed record FaturaResumoApi(long Id, long AssociacaoId,
    string Matricula, string Titular, string Clube,
    DateTime Competencia, DateTime DataVencimento,
    decimal ValorTotal, string Status, int DiasAtraso,
    bool BloqueioPorAtraso, decimal MultaPendente,
    decimal TotalAtualizado);

public sealed record ItemFaturaApi(long Id, string Tipo, string Descricao,
    decimal Valor, long? ReservaId, string? ClubeOrigem);
public sealed record FaturaDetalheApi(FaturaResumoApi Fatura,
    IReadOnlyList<ItemFaturaApi> Itens);
public sealed record EmissaoMensalidadeApi(long FaturaId, bool JaExistia);
public sealed record QuitacaoMensalidadeApi(long FaturaId,
    bool JaQuitada, decimal ValorQuitado, decimal MultaAplicada);

public sealed record LocacaoFinanceiraApi(
    long ReservaId, long UnidadePrestadoraId, string ClubePrestador,
    string ClubeOrigem, string Espaco, DateTime InicioLocal,
    decimal Principal, decimal Acrescimo, decimal Total,
    string SituacaoReserva, string SituacaoCobranca, string Modalidade,
    DateTime? CompetenciaDestino);
public sealed record ErroFinanceiroApi(string Erro);

public sealed class PoliticaFinanceiraFormulario
{
    [Range(1, long.MaxValue, ErrorMessage = "Selecione um clube.")]
    public long UnidadeId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Selecione uma categoria.")]
    public long CategoriaId { get; set; }

    [Range(typeof(decimal), "0", "9999999999.99",
        ErrorMessage = "Informe um valor mensal válido.")]
    public decimal ValorMensal { get; set; }

    [Required(ErrorMessage = "Informe a vigência.")]
    [DataType(DataType.Date)]
    public DateTime? VigenciaInicio { get; set; } =
        new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);

    public OpcoesFinanceirasApi Opcoes { get; set; } = new([], [], []);
}

public sealed class EmissaoMensalidadeFormulario
{
    [Range(1, long.MaxValue, ErrorMessage = "Selecione uma associação.")]
    public long AssociacaoId { get; set; }

    [Required(ErrorMessage = "Informe a competência.")]
    [DataType(DataType.Date)]
    public DateTime? Competencia { get; set; } =
        new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);

    public OpcoesFinanceirasApi Opcoes { get; set; } = new([], [], []);
}
