using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace ClickClub.Api.Seguranca;

public sealed record LoginSolicitacao(string Login, string Senha);

public sealed record UsuarioSessao(
    long Id, long PessoaId, string Nome, string Login,
    IReadOnlyList<PapelAcesso> Papeis);

public sealed record LoginResposta(
    string Token, DateTime ExpiraEmUtc, UsuarioSessao Usuario);

public sealed class AutenticacaoServico
{
    private readonly AcessoRepositorio _repositorio;
    private readonly IConfiguration _configuracao;
    private readonly IPasswordHasher<ContaAcesso> _hasher;

    public AutenticacaoServico(
        AcessoRepositorio repositorio,
        IConfiguration configuracao,
        IPasswordHasher<ContaAcesso> hasher)
    {
        _repositorio = repositorio;
        _configuracao = configuracao;
        _hasher = hasher;
    }

    public async Task<LoginResposta?> LoginAsync(
        string login, string senha, CancellationToken cancellationToken)
    {
        var conta = await _repositorio.BuscarPorLoginAsync(
            login.Trim(), cancellationToken);

        if (conta is null || !conta.PodeAutenticar || conta.Papeis.Count == 0)
            return null;

        var verificacao = _hasher.VerifyHashedPassword(
            conta, conta.SenhaHash, senha);

        if (verificacao == PasswordVerificationResult.Failed)
            return null;

        if (verificacao == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var novoHash = _hasher.HashPassword(conta, senha);
            await _repositorio.AtualizarSenhaHashAsync(
                conta.Id, novoHash, cancellationToken);
        }

        var agora = DateTime.UtcNow;
        var expiraEm = agora.AddMinutes(30);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, conta.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("name", conta.Login)
        };

        foreach (var papel in conta.Papeis.Select(p => p.Papel).Distinct())
            claims.Add(new Claim("role", papel));

        foreach (var escopo in conta.Papeis.Select(p => p.ChaveEscopo))
            claims.Add(new Claim("cc_escopo", escopo));

        var token = new JwtSecurityToken(
            issuer: "ClickClub.Api",
            audience: "ClickClub.Clientes",
            claims: claims,
            notBefore: agora,
            expires: expiraEm,
            signingCredentials: new SigningCredentials(
                ObterChaveAssinatura(_configuracao),
                SecurityAlgorithms.HmacSha256));

        return new LoginResposta(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiraEm,
            CriarUsuarioSessao(conta));
    }

    public static UsuarioSessao CriarUsuarioSessao(ContaAcesso conta) =>
        new(conta.Id, conta.PessoaId, conta.Nome, conta.Login, conta.Papeis);

    public static SymmetricSecurityKey ObterChaveAssinatura(
        IConfiguration configuracao)
    {
        var chaveBase64 = configuracao["Jwt:SigningKey"];
        byte[] chave;

        try
        {
            chave = Convert.FromBase64String(chaveBase64 ?? string.Empty);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey deve conter uma chave Base64 válida.");
        }

        if (chave.Length < 32)
        {
            throw new InvalidOperationException(
                "Configure Jwt:SigningKey com pelo menos 32 bytes aleatórios em User Secrets.");
        }

        return new SymmetricSecurityKey(chave);
    }
}
