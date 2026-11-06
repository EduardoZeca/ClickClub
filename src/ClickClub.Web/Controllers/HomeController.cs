using System.Diagnostics;
using ClickClub.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ClickClub.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IHttpClientFactory _clientFactory;

    public HomeController(
        ILogger<HomeController> logger,
        IHttpClientFactory clientFactory)
    {
        _logger = logger;
        _clientFactory = clientFactory;
    }

    public async Task<IActionResult> Index()
    {
        var client = _clientFactory.CreateClient("ClickClubApi");

        try
        {
            using var response = await client.GetAsync("api/diagnostico");

            ViewBag.ApiStatus = response.IsSuccessStatusCode
                ? "Online"
                : $"Indisponível (HTTP {(int)response.StatusCode})";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falha na comunicação com a API.");
            ViewBag.ApiStatus = "Indisponível (falha de conexão)";
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Tempo limite da API excedido.");
            ViewBag.ApiStatus = "Indisponível (tempo limite excedido)";
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None,
        NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}