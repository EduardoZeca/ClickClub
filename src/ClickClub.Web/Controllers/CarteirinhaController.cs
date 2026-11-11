using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClickClub.Web.Models;
using ClickClub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace ClickClub.Web.Controllers;

[Authorize(Roles = "Titular,Dependente")]
public sealed class CarteirinhaController(ClickClubApiClient api) : Controller
{
    [HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var pagina = new CarteirinhaPaginaViewModel();
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Get, "api/carteirinha/minha", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();

            if (resposta.IsSuccessStatusCode)
            {
                pagina.Credencial = await resposta.Content
                    .ReadFromJsonAsync<CarteirinhaApi>(cancellationToken: ct);
                if (pagina.Credencial is not null && pagina.CredencialValida)
                {
                    // Renderização local, sem enviar o QR a serviços externos.
                    var gerador = new QRCodeGenerator();
                    using var dadosQr = gerador.CreateQrCode(
                        pagina.Credencial.CodigoQr, QRCodeGenerator.ECCLevel.M);
                    using var renderizador = new PngByteQRCode(dadosQr);
                    var png = renderizador.GetGraphic(6);
                    pagina.ImagemQrBase64 = Convert.ToBase64String(png);
                }
            }
            else if (resposta.StatusCode == HttpStatusCode.NotFound)
            {
                pagina.Erro = "Nenhuma carteirinha válida disponível. Solicite a emissão ou renovação.";
            }
            else
            {
                pagina.Erro = "Não foi possível consultar a carteirinha na API.";
            }

            using var associacao = await api.EnviarAsync(
                HttpMethod.Get, "api/minha-associacao", null, ct);
            if (associacao.IsSuccessStatusCode)
                pagina.Associacao = await associacao.Content
                    .ReadFromJsonAsync<MinhaAssociacaoApi>(cancellationToken: ct);
        }
        catch (HttpRequestException)
        {
            pagina.Credencial = null;
            pagina.ImagemQrBase64 = null;
            pagina.Erro = "API indisponível. Não foi possível conferir a carteirinha.";
        }
        catch (TaskCanceledException)
        {
            pagina.Credencial = null;
            pagina.ImagemQrBase64 = null;
            pagina.Erro = "Tempo limite da API excedido. Tente novamente conectado.";
        }
        catch (JsonException)
        {
            pagina.Credencial = null;
            pagina.ImagemQrBase64 = null;
            pagina.Erro = "Resposta de carteirinha inválida.";
        }

        return View(pagina);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renovar(CancellationToken ct)
    {
        try
        {
            using var resposta = await api.EnviarAsync(
                HttpMethod.Post, "api/carteirinha/minha/renovar", null, ct);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToAction("Entrar", "Conta");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
                return Forbid();
            if (resposta.IsSuccessStatusCode)
            {
                TempData["Sucesso"] = "Carteirinha consultada ou renovada. A portaria verificará o QR online.";
            }
            else if (resposta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict)
            {
                var erro = await resposta.Content.ReadFromJsonAsync<ErroApi>(
                    cancellationToken: ct);
                TempData["Erro"] = erro?.Erro ?? "Não foi possível emitir a carteirinha.";
            }
            else
            {
                TempData["Erro"] = "Não foi possível emitir ou renovar a carteirinha.";
            }
        }
        catch (HttpRequestException)
        {
            TempData["Erro"] = "API indisponível. Não foi possível emitir a carteirinha.";
        }
        catch (TaskCanceledException)
        {
            TempData["Erro"] = "Tempo limite da API excedido.";
        }
        catch (JsonException)
        {
            TempData["Erro"] = "Resposta inesperada ao renovar a carteirinha.";
        }

        return RedirectToAction(nameof(Index));
    }
}
