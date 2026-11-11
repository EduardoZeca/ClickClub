using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClickClub.Mobile.Modelos;

namespace ClickClub.Mobile.Servicos;

public sealed partial class ApiClickClub
{
    private readonly SessaoLocal _sessao;
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(ApiConfiguracao.UrlBase),
        Timeout = TimeSpan.FromSeconds(8)
    };

    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    public ApiClickClub(SessaoLocal sessao) => _sessao = sessao;

    public async Task<(bool Ok, string Mensagem)> EntrarAsync(
        string login, string senha, CancellationToken ct = default)
    {
        try
        {
            using var resposta = await _http.PostAsJsonAsync(
                "api/autenticacao/login", new { login, senha }, ct);
            if (!resposta.IsSuccessStatusCode)
                return (false, await MensagemErroAsync(resposta, ct));

            var conteudo = await resposta.Content.ReadAsStringAsync(ct);
            var token = ExtrairJwt(conteudo);
            if (string.IsNullOrWhiteSpace(token))
                return (false, "Resposta de login sem token JWT reconhecível. Conferir contrato da API.");

            // Consulta de identidade no servidor antes de manter a nova sessão.
            using var consulta = new HttpRequestMessage(HttpMethod.Get, "api/autenticacao/me");
            consulta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var identidade = await _http.SendAsync(consulta, ct);
            if (!identidade.IsSuccessStatusCode)
                return (false, "Não foi possível validar a sessão na API.");

            await _sessao.GuardarTokenAsync(token);
            return (true, "Login realizado.");
        }
        catch (HttpRequestException) { return (false, "API inacessível. Verifique o servidor e o ADB reverse."); }
        catch (TaskCanceledException) { return (false, "Tempo limite de comunicação com a API."); }
        catch (JsonException) { return (false, "Resposta de autenticação inválida."); }
        catch (Exception) { return (false, "Falha ao iniciar sessão. Verifique o armazenamento seguro do aparelho."); }
    }

    public async Task<(CarteirinhaApi? Credencial, HttpStatusCode? Status, string Mensagem)>
        AtualizarCarteirinhaAsync(CancellationToken ct = default)
    {
        try
        {
            var token = await _sessao.LerTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
                return (null, HttpStatusCode.Unauthorized, "Entre novamente para atualizar a carteirinha.");

            using var requisicao = new HttpRequestMessage(
                HttpMethod.Post, "api/carteirinha/minha/renovar");
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resposta = await _http.SendAsync(requisicao, ct);
            if (!resposta.IsSuccessStatusCode)
                return (null, resposta.StatusCode, await MensagemErroAsync(resposta, ct));

            var credencial = await resposta.Content.ReadFromJsonAsync<CarteirinhaApi>(Opcoes, ct);
            if (credencial is null || !credencial.EstaValida)
                return (null, resposta.StatusCode, "A API não retornou uma credencial válida.");
            return (credencial, resposta.StatusCode, "Carteirinha atualizada.");
        }
        catch (HttpRequestException) { return (null, null, "Sem conexão com a API; exibindo cache, quando válido."); }
        catch (TaskCanceledException) { return (null, null, "A API não respondeu; exibindo cache, quando válido."); }
        catch (JsonException) { return (null, null, "Resposta de carteirinha inválida."); }
    }

    private static async Task<string> MensagemErroAsync(HttpResponseMessage resposta,
        CancellationToken ct)
    {
        if (resposta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return "Acesso não autorizado. Confira seu perfil e situação associativa.";
        try
        {
            var corpo = await resposta.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(corpo);
            var raiz = doc.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object)
            {
                foreach (var item in raiz.EnumerateObject())
                {
                    if ((item.Name.Equals("erro", StringComparison.OrdinalIgnoreCase) ||
                         item.Name.Equals("mensagem", StringComparison.OrdinalIgnoreCase)) &&
                        item.Value.ValueKind == JsonValueKind.String)
                        return item.Value.GetString() ?? "Operação rejeitada pela API.";
                }
            }
        }
        catch (JsonException) { }
        return $"Requisição não concluída (HTTP {(int)resposta.StatusCode}).";
    }

    private static string? ExtrairJwt(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Buscar(doc.RootElement);

        static string? Buscar(JsonElement elemento)
        {
            if (elemento.ValueKind == JsonValueKind.String)
            {
                var valor = elemento.GetString();
                return valor is not null && valor.Split('.').Length == 3 ? valor : null;
            }
            if (elemento.ValueKind != JsonValueKind.Object) return null;
            string[] nomes = ["token", "tokenAcesso", "accessToken", "access_token", "jwt", "tokenJwt"];
            foreach (var nome in nomes)
            {
                foreach (var propriedade in elemento.EnumerateObject())
                {
                    if (propriedade.Name.Equals(nome, StringComparison.OrdinalIgnoreCase))
                    {
                        var token = Buscar(propriedade.Value);
                        if (token is not null) return token;
                    }
                }
            }
            foreach (var propriedade in elemento.EnumerateObject())
            {
                var token = Buscar(propriedade.Value);
                if (token is not null) return token;
            }
            return null;
        }
    }
}
