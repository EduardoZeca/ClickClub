using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ClickClub.Api.Controllers;

[ApiController]
[Route("api/diagnostico")]
public class DiagnosticoController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiagnosticoController> _logger;
    private readonly IWebHostEnvironment _environment;

    public DiagnosticoController(
        IConfiguration configuration,
        ILogger<DiagnosticoController> logger,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    [HttpGet]
    public IActionResult Consultar()
    {
        return Ok(new
        {
            sistema = "ClickClub",
            status = "online"
        });
    }

    [HttpGet("banco")]
    public async Task<IActionResult> ConsultarBanco(
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var connectionString = _configuration
            .GetConnectionString("ClickClub");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Problem(
                "A conexão com o banco não foi configurada.");
        }

        try
        {
            await using var conexao =
                new MySqlConnection(connectionString);

            await conexao.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                "SELECT COUNT(*) FROM categoria_associativa",
                conexao);

            var resultado = await comando
                .ExecuteScalarAsync(cancellationToken);

            return Ok(new
            {
                sistema = "ClickClub",
                banco = "online",
                categorias = Convert.ToInt32(resultado)
            });
        }
        catch (MySqlException ex)
        {
            _logger.LogError(
                ex,
                "Falha na conexão com o MySQL.");

            return StatusCode(503, new
            {
                sistema = "ClickClub",
                banco = "indisponivel"
            });
        }
    }
}