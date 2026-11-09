using System.ComponentModel.DataAnnotations;

namespace ClickClub.Api.Contratos;

public sealed class AdesaoSolicitacaoEntrada
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [Required, RegularExpression("^[0-9]{11}$")]
    public string Cpf { get; set; } = string.Empty;

    [DataType(DataType.Date)] public DateTime? DataNascimento { get; set; }
    [StringLength(255)] public string? Endereco { get; set; }
    [StringLength(120)] public string? Bairro { get; set; }
    [StringLength(120)] public string? Cidade { get; set; }
    [RegularExpression("^([A-Za-z]{2})?$")]
    public string? Uf { get; set; }
    [StringLength(40)] public string? Sexo { get; set; }
    [StringLength(40)] public string? EstadoCivil { get; set; }
    [StringLength(30)] public string? Telefone { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }

    [Range(1, long.MaxValue)] public long UnidadePretendidaId { get; set; }
    [Range(1, long.MaxValue)] public long TipoVinculoId { get; set; }

    public List<string> Interesses { get; set; } = [];
}

public sealed record AdesaoSolicitacaoCriada(
    long Id, string CodigoConsulta, string Status);

public sealed class AdesaoConsultaEntrada
{
    [Required, RegularExpression("^[A-Za-z0-9_-]{43}$")]
    public string Codigo { get; set; } = string.Empty;
}

public sealed record AdesaoConsultaResposta(
    long Id, string Status, string Clube, string TipoVinculo,
    DateTime SolicitadaEmUtc, DateTime? AnalisadaEmUtc, string? Motivo);

public sealed record UnidadeAdesaoOpcao(long Id, string Nome);
public sealed record TipoAdesaoOpcao(long Id, string Nome);
public sealed record AdesaoOpcoesPublicas(
    IReadOnlyList<UnidadeAdesaoOpcao> Clubes,
    IReadOnlyList<TipoAdesaoOpcao> TiposVinculo);

public sealed record AdesaoResumo(
    long Id, string Nome, string CpfFinal, string Clube,
    string TipoVinculo, string Status, DateTime SolicitadaEmUtc);

public sealed record AdesaoDetalhe(
    long Id, string Nome, string Cpf, string? Email, string? Telefone,
    long PessoaId, long UnidadePretendidaId, string Clube,
    long TipoVinculoId, string TipoVinculo, string Status,
    DateTime SolicitadaEmUtc, DateTime? AnalisadaEmUtc,
    string? Motivo);

public sealed record CategoriaAvaliacaoOpcao(long Id, string Nome);

public sealed class AdesaoDecisaoEntrada
{
    public bool Aprovar { get; set; }
    public long? CategoriaId { get; set; }
    [StringLength(30)] public string? Matricula { get; set; }
    [StringLength(500)] public string? Motivo { get; set; }
}

public enum ResultadoDecisaoAdesao
{
    Concluida, NaoEncontrada, ForaDoEscopo, JaAnalisada, DadosInvalidos
}
