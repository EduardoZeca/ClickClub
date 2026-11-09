namespace ClickClub.Api.Seguranca;

public sealed record PapelAcesso(string Papel, long? UnidadeId)
{
    public string ChaveEscopo => $"{Papel}:{UnidadeId?.ToString() ?? "*"}";
}

public sealed class ContaAcesso
{
    public long Id { get; init; }
    public long PessoaId { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Login { get; init; } = string.Empty;
    public string SenhaHash { get; init; } = string.Empty;
    public bool Ativo { get; init; }
    public string StatusPessoa { get; init; } = string.Empty;
    public IReadOnlyList<PapelAcesso> Papeis { get; set; } = [];

    public bool PodeAutenticar => Ativo && StatusPessoa == "Ativa";
}
