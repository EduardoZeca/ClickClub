using System.IdentityModel.Tokens.Jwt;
using ClickClub.Api.Dados;
using ClickClub.Api.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/autenticacao")]
public sealed class AutenticacaoController : ControllerBase
{
    private readonly AutenticacaoServico _servico;
    private readonly AcessoRepositorio _repositorio;

    public AutenticacaoController(
        AutenticacaoServico servico,
        AcessoRepositorio repositorio)
    {
        _servico = servico;
        _repositorio = repositorio;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(solicitacao.Login) ||
            solicitacao.Login.Length > 100 ||
            string.IsNullOrEmpty(solicitacao.Senha))
        {
            return BadRequest(new { erro = "Login e senha são obrigatórios." });
        }

        var resposta = await _servico.LoginAsync(
            solicitacao.Login, solicitacao.Senha, cancellationToken);

        return resposta is null
            ? Unauthorized(new { erro = "Credenciais inválidas." })
            : Ok(resposta);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> ConsultarSessao(
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            out var usuarioId))
        {
            return Unauthorized();
        }

        var conta = await _repositorio.BuscarPorIdAsync(
            usuarioId, cancellationToken);

        return conta is null || !conta.PodeAutenticar
            ? Unauthorized()
            : Ok(AutenticacaoServico.CriarUsuarioSessao(conta));
    }
}
