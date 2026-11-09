using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ClickClub.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

public sealed class ContaController(IHttpClientFactory clientFactory) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Entrar(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(
        LoginViewModel entrada, string? returnUrl, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(entrada);

        try
        {
            var client = clientFactory.CreateClient("ClickClubApi");
            using var resposta = await client.PostAsJsonAsync(
                "api/autenticacao/login",
                new { entrada.Login, entrada.Senha }, ct);

            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
            {
                ModelState.AddModelError(string.Empty, "Login ou senha inválidos.");
                return View(entrada);
            }

            if (!resposta.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty,
                    "Não foi possível autenticar. Verifique se a API está disponível.");
                return View(entrada);
            }

            var sessao = await resposta.Content
                .ReadFromJsonAsync<LoginRespostaApi>(cancellationToken: ct);

            if (sessao is null || string.IsNullOrWhiteSpace(sessao.Token) ||
                sessao.Usuario is null)
            {
                ModelState.AddModelError(string.Empty, "Resposta de login inválida.");
                return View(entrada);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, sessao.Usuario.Id.ToString()),
                new(ClaimTypes.Name, sessao.Usuario.Nome)
            };

            foreach (var papel in sessao.Usuario.Papeis.Select(p => p.Papel).Distinct())
                claims.Add(new Claim(ClaimTypes.Role, papel));

            var identidade = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var propriedades = new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = false,
                ExpiresUtc = new DateTimeOffset(
                    DateTime.SpecifyKind(sessao.ExpiraEmUtc, DateTimeKind.Utc))
            };

            propriedades.StoreTokens(
            [
                new AuthenticationToken
                {
                    Name = "access_token", Value = sessao.Token
                }
            ]);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identidade), propriedades);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return sessao.Usuario.Papeis.Any(p => p.Papel == "AdministradorGeral")
                ? RedirectToAction("Index", "Unidades")
                : RedirectToAction("Index", "Home");
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "API indisponível.");
            return View(entrada);
        }
        catch (TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty, "Tempo limite da API excedido.");
            return View(entrada);
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Entrar));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AcessoNegado() => View();
}
