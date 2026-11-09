using System.ComponentModel.DataAnnotations;

namespace ClickClub.Web.Models;

public sealed record ClubeAdesaoOpcao(long Id, string Nome);
public sealed record TipoAdesaoOpcao(long Id, string Nome);
public sealed record OpcoesAdesaoApi(
    IReadOnlyList<ClubeAdesaoOpcao> Clubes,
    IReadOnlyList<TipoAdesaoOpcao> TiposVinculo);

public sealed class SolicitacaoAdesaoFormulario
{
    [Required(ErrorMessage = "Informe seu nome."), StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o CPF (11 números)."), RegularExpression("^[0-9]{11}$",
        ErrorMessage = "CPF deve conter 11 números, sem pontuação.")]
    public string Cpf { get; set; } = string.Empty;

    [DataType(DataType.Date)] public DateTime? DataNascimento { get; set; }
    [StringLength(255)] public string? Endereco { get; set; }
    [StringLength(120)] public string? Bairro { get; set; }
    [StringLength(120)] public string? Cidade { get; set; }
    [RegularExpression("^([A-Za-z]{2})?$", ErrorMessage = "UF deve ter duas letras.")]
    public string? Uf { get; set; }
    [StringLength(40)] public string? Sexo { get; set; }
    [StringLength(40)] public string? EstadoCivil { get; set; }
    [StringLength(30)] public string? Telefone { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Selecione um clube.")]
    public long UnidadePretendidaId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Selecione um tipo de vínculo.")]
    public long TipoVinculoId { get; set; }

    [StringLength(1000)] public string? InteressesTexto { get; set; }

    public IReadOnlyList<ClubeAdesaoOpcao> Clubes { get; set; } = [];
    public IReadOnlyList<TipoAdesaoOpcao> TiposVinculo { get; set; } = [];

    public object ParaApi() => new
    {
        Nome, Cpf, DataNascimento, Endereco, Bairro, Cidade, Uf,
        Sexo, EstadoCivil, Telefone, Email, UnidadePretendidaId,
        TipoVinculoId,
        Interesses = (InteressesTexto ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray()
    };
}

public sealed record AdesaoCriadaApi(long Id, string CodigoConsulta, string Status);

public sealed class ConsultaAdesaoFormulario
{
    [Required(ErrorMessage = "Informe o código de consulta."),
     RegularExpression("^[A-Za-z0-9_-]{43}$",
         ErrorMessage = "Código inválido (43 caracteres).")]
    public string Codigo { get; set; } = string.Empty;

    public AdesaoConsultaApi? Resultado { get; set; }
}

public sealed record AdesaoConsultaApi(
    long Id, string Status, string Clube, string TipoVinculo,
    DateTime SolicitadaEmUtc, DateTime? AnalisadaEmUtc, string? Motivo);

public sealed record AdesaoResumoApi(
    long Id, string Nome, string CpfFinal, string Clube,
    string TipoVinculo, string Status, DateTime SolicitadaEmUtc);

public sealed record AdesaoDetalheApi(
    long Id, string Nome, string Cpf, string? Email, string? Telefone,
    long PessoaId, long UnidadePretendidaId, string Clube,
    long TipoVinculoId, string TipoVinculo, string Status,
    DateTime SolicitadaEmUtc, DateTime? AnalisadaEmUtc, string? Motivo);

public sealed record CategoriaAdesaoOpcao(long Id, string Nome);

public sealed class DecisaoAdesaoFormulario
{
    [Range(1, long.MaxValue)] public long Id { get; set; }
    public bool Aprovar { get; set; }
    public long? CategoriaId { get; set; }
    [StringLength(30)] public string? Matricula { get; set; }
    [StringLength(500)] public string? Motivo { get; set; }
}

public sealed record AdesaoDetalhesPagina(
    AdesaoDetalheApi Solicitacao,
    IReadOnlyList<CategoriaAdesaoOpcao> Categorias);
