using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class EspacoEntrada
{
    [Range(1, long.MaxValue)]
    public long UnidadeId { get; set; }

    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [Required, RegularExpression("^(SalaoFestas|Churrasqueira|Quadra)$")]
    public string Tipo { get; set; } = string.Empty;

    [Range(1, 10000)]
    public int Capacidade { get; set; }

    [Range(typeof(decimal), "0", "9999999999.99")]
    public decimal TaxaBase { get; set; }
}

public sealed class EspacoSituacaoEntrada
{
    public bool Ativo { get; set; }
}

public sealed record EspacoResumo(
    long Id, long UnidadeId, string Clube, string Nome,
    string Tipo, int Capacidade, decimal TaxaBase, bool Ativo);

public sealed record ClubeEspacoOpcao(long Id, string Nome);

public enum ResultadoEspaco
{
    Concluido, NaoEncontrado, ForaDoEscopo, UnidadeInativa,
    UnidadeNaoPodeSerAlterada
}
