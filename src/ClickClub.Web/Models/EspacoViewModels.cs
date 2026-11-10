using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed record EspacoApi(
    long Id, long UnidadeId, string Clube, string Nome,
    string Tipo, int Capacidade, decimal TaxaBase, bool Ativo);

public sealed record ClubeEspacoOpcaoApi(long Id, string Nome);

public sealed class EspacoFormulario
{
    public long Id { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Selecione um clube.")]
    public long UnidadeId { get; set; }

    [Required(ErrorMessage = "Informe o nome do espaço."),
     StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione o tipo."),
     RegularExpression("^(SalaoFestas|Churrasqueira|Quadra)$")]
    public string Tipo { get; set; } = string.Empty;

    [Range(1, 10000, ErrorMessage = "Capacidade deve estar entre 1 e 10.000.")]
    public int Capacidade { get; set; }

    [Range(typeof(decimal), "0", "9999999999.99",
        ErrorMessage = "A taxa não pode ser negativa.")]
    public decimal TaxaBase { get; set; }

    public IReadOnlyList<ClubeEspacoOpcaoApi> Clubes { get; set; } = [];

    public static EspacoFormulario De(EspacoApi e) => new()
    {
        Id = e.Id, UnidadeId = e.UnidadeId,
        Nome = e.Nome, Tipo = e.Tipo,
        Capacidade = e.Capacidade, TaxaBase = e.TaxaBase
    };
}
