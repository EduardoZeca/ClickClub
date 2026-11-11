using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using ClickClub.Api.Dados;
using ClickClub.Api.Seguranca;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

// Gera a chave privada ECDSA no próprio computador do desenvolvedor.
// A saída deve ser direcionada para User Secrets, nunca versionada no Git.
if (args.Contains("--gerar-chave-carteirinha", StringComparer.OrdinalIgnoreCase))
{
    using var chave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    Console.WriteLine(Convert.ToBase64String(chave.ExportPkcs8PrivateKey()));
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddScoped<AcessoRepositorio>();
builder.Services.AddScoped<UnidadeRepositorio>();
builder.Services.AddScoped<CategoriaRepositorio>();
builder.Services.AddScoped<TitularRepositorio>();
builder.Services.AddScoped<DependenteRepositorio>();
builder.Services.AddScoped<ContaAdministrativaRepositorio>();
builder.Services.AddScoped<MinhaAssociacaoRepositorio>();
builder.Services.AddScoped<EquipeRepositorio>();
builder.Services.AddScoped<AdesaoRepositorio>();
builder.Services.AddScoped<EspacoRepositorio>();
builder.Services.AddScoped<AgendaEspacoRepositorio>();
builder.Services.AddScoped<ReservaRepositorio>();
builder.Services.AddScoped<ConvidadosReservaRepositorio>();
builder.Services.AddScoped<CancelamentoReservaRepositorio>();
builder.Services.AddScoped<PermissaoReservaDependenteRepositorio>();
builder.Services.AddScoped<PoliticaFinanceiraRepositorio>();
builder.Services.AddScoped<FaturaRepositorio>();
builder.Services.AddScoped<QuitacaoMensalidadeRepositorio>();
builder.Services.AddScoped<CobrancaLocacaoRepositorio>();
builder.Services.AddScoped<FinanceiroLocacoesRepositorio>();
builder.Services.AddScoped<CarteirinhaRepositorio>();
builder.Services.AddScoped<PortariaRepositorio>();
builder.Services.AddScoped<AtividadeRepositorio>();
builder.Services.AddScoped<TurmaRepositorio>();
builder.Services.AddScoped<InscricaoTurmaRepositorio>();
builder.Services.AddScoped<EventoSocialRepositorio>();
builder.Services.AddScoped<InscricaoEventoRepositorio>();
builder.Services.AddScoped<VisitanteEventoRepositorio>();
builder.Services.AddScoped<PortariaEventoRepositorio>();
builder.Services.AddScoped<AvisoRepositorio>();
builder.Services.AddScoped<OuvidoriaRepositorio>();
builder.Services.AddScoped<DashboardRepositorio>();
builder.Services.AddSingleton<CarteirinhaAssinaturaServico>();
builder.Services.AddScoped<AutenticacaoServico>();
builder.Services.AddSingleton<IPasswordHasher<ContaAcesso>, PasswordHasher<ContaAcesso>>();

// Limita pedidos anônimos de adesão/consulta. A API nunca registra o código privado.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("AdesaoPublica", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

var chaveJwt = AutenticacaoServico.ObterChaveAssinatura(
    builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "ClickClub.Api",
            ValidateAudience = true,
            ValidAudience = "ClickClub.Clientes",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = chaveJwt,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };

        // Uma conta desativada ou uma mudança nos papéis/escopos
        // invalida o token, mesmo antes de seus 30 minutos.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async contexto =>
            {
                var id = contexto.Principal?
                    .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                if (!long.TryParse(id, out var usuarioId))
                {
                    contexto.Fail("Identificação inválida.");
                    return;
                }

                try
                {
                    var repositorio = contexto.HttpContext.RequestServices
                        .GetRequiredService<AcessoRepositorio>();
                    var conta = await repositorio.BuscarPorIdAsync(
                        usuarioId, contexto.HttpContext.RequestAborted);

                    var escoposToken = contexto.Principal!
                        .FindAll("cc_escopo")
                        .Select(claim => claim.Value)
                        .ToHashSet(StringComparer.Ordinal);

                    var escoposAtuais = conta?.Papeis
                        .Select(papel => papel.ChaveEscopo)
                        .ToHashSet(StringComparer.Ordinal);

                    if (conta is null || !conta.PodeAutenticar ||
                        escoposAtuais is null ||
                        !escoposToken.SetEquals(escoposAtuais))
                    {
                        contexto.Fail("Sessão não autorizada.");
                    }
                }
                catch (Exception)
                {
                    // Não autorizar por cache se o MySQL estiver indisponível.
                    contexto.Fail("Não foi possível validar a sessão.");
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SomenteAdministradorGeral",
        policy => policy.RequireRole("AdministradorGeral"));
});

var app = builder.Build();

if (args.Contains("--bootstrap-admin", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    await AdministradorInicial.ExecutarAsync(scope.ServiceProvider);
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
