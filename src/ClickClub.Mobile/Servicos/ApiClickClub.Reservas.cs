using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClickClub.Mobile.Modelos;

namespace ClickClub.Mobile.Servicos;

// Integração restrita ao Mobile. A API permanece responsável por cotas, horários,
// inadimplência, autorizações de dependentes, concorrência e cancelamentos.
public sealed partial class ApiClickClub
{
    // Centralize aqui qualquer ajuste de caminho após conferência dos Controllers C2/C3.
    private static readonly string[] RotasClubes = ["api/clubes", "api/unidades"];
    private const string RotaEspacos = "api/espacos";
    private const string RotaReservas = "api/reservas";

    public Task<(List<ClubeMobile>? Itens, string Mensagem)> ClubesAsync(
        CancellationToken ct = default) => ListarAsync(RotasClubes, LerClube, ct);

    public async Task<(List<EspacoMobile>? Itens, string Mensagem)> EspacosAsync(
        long unidadeId, CancellationToken ct = default)
    {
        if (unidadeId <= 0) return (null, "Clube inválido.");
        var (itens, erro) = await ListarAsync(
            [$"{RotaEspacos}?unidadeId={unidadeId}"], LerEspaco, ct);
        if (itens is null) return (null, erro);
        return (itens.Where(x => x.UnidadeId == 0 || x.UnidadeId == unidadeId)
            .Where(x => x.Ativo).ToList(), erro);
    }

    public Task<(List<ReservaMobile>? Itens, string Mensagem)> MinhasReservasAsync(
        CancellationToken ct = default) =>
        ListarAsync([$"{RotaReservas}/minhas"], LerReserva, ct);

    public async Task<(bool Ok, string Mensagem)> CriarReservaAsync(
        NovaReservaMobile entrada, CancellationToken ct = default)
    {
        if (entrada.EspacoId <= 0 || entrada.FimLocal <= entrada.InicioLocal)
            return (false, "Informe um espaço e um período válido.");

        // Horários locais SEM offset: a API converte usando o fuso do clube.
        var corpo = new
        {
            espacoId = entrada.EspacoId,
            inicioLocal = entrada.InicioLocal.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            fimLocal = entrada.FimLocal.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            finalidade = entrada.Finalidade,
            convidados = entrada.Convidados.Select(x => new
            {
                nome = x.Nome,
                identificacaoMinima = x.IdentificacaoMinima
            }).ToArray()
        };

        return await AlterarReservaAsync(HttpMethod.Post, RotaReservas, corpo, ct);
    }

    public async Task<(bool Ok, string Mensagem)> CancelarReservaAsync(
        long reservaId, CancellationToken ct = default)
    {
        if (reservaId <= 0) return (false, "Reserva inválida.");
        // Não repetir um cancelamento automaticamente: uma falha de rede pode
        // ocorrer depois de a operação ter sido processada no servidor.
        return await AlterarReservaAsync(HttpMethod.Post,
            $"{RotaReservas}/{reservaId}/cancelar",
            new { motivo = "Cancelamento solicitado pelo aplicativo." }, ct);
    }

    private async Task<(bool Ok, string Mensagem)> AlterarReservaAsync(
        HttpMethod metodo, string caminho, object corpo, CancellationToken ct)
    {
        try
        {
            var token = await _sessao.LerTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
                return (false, "Sessão ausente. Entre novamente.");
            using var pedido = new HttpRequestMessage(metodo, caminho)
            {
                Content = JsonContent.Create(corpo)
            };
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resposta = await _http.SendAsync(pedido, ct);
            return resposta.IsSuccessStatusCode
                ? (true, "Operação confirmada pela API.")
                : (false, await MensagemErroAsync(resposta, ct));
        }
        catch (HttpRequestException) { return (false, "API indisponível. Não é possível confirmar a operação."); }
        catch (TaskCanceledException) { return (false, "A API não respondeu. Consulte o histórico antes de tentar novamente."); }
        catch (Exception) { return (false, "Falha na operação. Consulte o histórico antes de repetir."); }
    }

    private async Task<(List<T>? Itens, string Mensagem)> ListarAsync<T>(
        IReadOnlyList<string> caminhos, Func<JsonElement, T?> converter,
        CancellationToken ct) where T : class
    {
        try
        {
            var token = await _sessao.LerTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
                return (null, "Sessão ausente. Entre novamente.");

            for (var i = 0; i < caminhos.Count; i++)
            {
                using var pedido = new HttpRequestMessage(HttpMethod.Get, caminhos[i]);
                pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var resposta = await _http.SendAsync(pedido, ct);
                if (resposta.StatusCode == HttpStatusCode.NotFound && i + 1 < caminhos.Count)
                    continue; // fallback SOMENTE para consultas; nunca para gravações.
                if (!resposta.IsSuccessStatusCode)
                    return (null, await MensagemErroAsync(resposta, ct));

                var json = await resposta.Content.ReadAsStringAsync(ct);
                using var documento = JsonDocument.Parse(json);
                var colecao = EncontrarColecao(documento.RootElement);
                if (colecao.ValueKind != JsonValueKind.Array)
                    return (null, "Formato da lista não reconhecido. Confira o contrato da API.");
                var itens = new List<T>();
                foreach (var item in colecao.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var valor = converter(item);
                    if (valor is not null) itens.Add(valor);
                }
                return (itens, "Consulta concluída.");
            }
            return (null, "Endpoint não encontrado. Confira os caminhos da API.");
        }
        catch (HttpRequestException) { return (null, "API indisponível. Conecte-se para consultar reservas."); }
        catch (TaskCanceledException) { return (null, "Tempo limite de comunicação com a API."); }
        catch (JsonException) { return (null, "A API retornou dados em formato inesperado."); }
        catch (Exception) { return (null, "Não foi possível consultar os dados."); }
    }

    private static JsonElement EncontrarColecao(JsonElement elemento)
    {
        if (elemento.ValueKind == JsonValueKind.Array) return elemento;
        if (elemento.ValueKind != JsonValueKind.Object) return default;
        var candidata = Propriedade(elemento, "itens", "dados", "resultados",
            "reservas", "clubes", "unidades", "espacos", "items",
            "faturas", "turmas", "eventos", "inscricoes");
        if (candidata.ValueKind == JsonValueKind.Array) return candidata;
        if (candidata.ValueKind == JsonValueKind.Object)
            return EncontrarColecao(candidata);
        return default;
    }

    private static JsonElement Propriedade(JsonElement objeto, params string[] nomes)
    {
        if (objeto.ValueKind != JsonValueKind.Object) return default;
        foreach (var nome in nomes)
            foreach (var propriedade in objeto.EnumerateObject())
                if (propriedade.Name.Equals(nome, StringComparison.OrdinalIgnoreCase))
                    return propriedade.Value;
        return default;
    }

    private static string Texto(JsonElement objeto, params string[] nomes)
    {
        var p = Propriedade(objeto, nomes);
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString() ?? "",
            JsonValueKind.Number => p.ToString(),
            JsonValueKind.True => "Sim",
            JsonValueKind.False => "Não",
            _ => ""
        };
    }

    private static long Inteiro(JsonElement objeto, params string[] nomes)
    {
        var p = Propriedade(objeto, nomes);
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var valor)) return valor;
        return p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), out valor)
            ? valor : 0;
    }

    private static decimal Valor(JsonElement objeto, params string[] nomes)
    {
        var p = Propriedade(objeto, nomes);
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var numero)) return numero;
        if (p.ValueKind == JsonValueKind.String &&
            decimal.TryParse(p.GetString(), NumberStyles.Number,
                CultureInfo.InvariantCulture, out numero)) return numero;
        return 0m;
    }

    private static bool Ativo(JsonElement objeto)
    {
        var p = Propriedade(objeto, "ativo", "ativa");
        return p.ValueKind switch
        {
            JsonValueKind.False => false,
            JsonValueKind.Number => p.TryGetInt32(out var flag) && flag != 0,
            JsonValueKind.String => !string.Equals(p.GetString(), "false",
                StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.GetString(), "Inativo", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static ClubeMobile? LerClube(JsonElement item)
    {
        var id = Inteiro(item, "id", "unidadeId", "clubeId");
        if (id <= 0) return null;
        return new ClubeMobile(id, Texto(item, "nome", "nomeClube", "nomeUnidade"),
            Texto(item, "municipio", "cidade"), Texto(item, "uf", "estado"),
            Texto(item, "fusoHorario"));
    }

    private static EspacoMobile? LerEspaco(JsonElement item)
    {
        var id = Inteiro(item, "id", "espacoId");
        if (id <= 0) return null;
        return new EspacoMobile(id, Inteiro(item, "unidadeId", "clubeId"),
            Texto(item, "nome", "nomeEspaco"), Texto(item, "tipo", "tipoEspaco"),
            (int)Math.Clamp(Inteiro(item, "capacidade"), 0, int.MaxValue),
            Valor(item, "taxaBase", "taxa", "valor", "preco"), Ativo(item));
    }

    private static ReservaMobile? LerReserva(JsonElement item)
    {
        var id = Inteiro(item, "id", "reservaId");
        if (id <= 0) return null;
        return new ReservaMobile(id, Texto(item, "espacoNome", "nomeEspaco", "espaco"),
            Texto(item, "unidadeNome", "clubeNome", "nomeClube"),
            Horario(item, "inicioLocal", "inicio", "inicioUtc"),
            Horario(item, "fimLocal", "fim", "fimUtc"),
            Texto(item, "status", "situacao"),
            Valor(item, "valorContratado", "taxa", "valorTotal", "valor"),
            Texto(item, "finalidade"));
    }

    private static string Horario(JsonElement objeto, params string[] nomes)
    {
        var valor = Texto(objeto, nomes);
        if (DateTime.TryParse(valor, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var data))
            return data.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
        return string.IsNullOrWhiteSpace(valor) ? "Não informado" : valor;
    }
}
