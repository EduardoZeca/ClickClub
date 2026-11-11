using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClickClub.Mobile.Modelos;

namespace ClickClub.Mobile.Servicos;

// Os endpoints de turmas e eventos foram conferidos nos Controllers do C6.
// Endpoints financeiros dependem de conferência no FinanceiroController C4,
// indisponível nesta sessão. Não realizar fallback automático em POST/DELETE.
public sealed partial class ApiClickClub
{
    private const string Faturas = "api/financeiro/faturas"; // contrato pendente C4
    private const string Turmas = "api/turmas";
    private const string Eventos = "api/eventos";

    public Task<(List<FaturaMobile>? Itens, string Mensagem)> MinhasFaturasAsync(
        CancellationToken ct = default) =>
        ListarAsync([Faturas], LerFatura, ct);

    public async Task<(FaturaDetalheMobile? Detalhe, string Mensagem)> DetalharFaturaAsync(
        long id, CancellationToken ct = default)
    {
        if (id <= 0) return (null, "Fatura inválida.");
        var (elemento, erro) = await ObterJsonAsync($"{Faturas}/{id}", ct);
        if (elemento is null) return (null, erro);
        var raiz = elemento.Value;
        var cabecalho = Propriedade(raiz, "fatura");
        if (cabecalho.ValueKind != JsonValueKind.Object) cabecalho = raiz;
        var fatura = LerFatura(cabecalho);
        if (fatura is null) return (null, "A API retornou detalhes incompletos da fatura.");
        var itensJson = Propriedade(raiz, "itens", "itensFatura");
        if (itensJson.ValueKind != JsonValueKind.Array)
            itensJson = Propriedade(cabecalho, "itens", "itensFatura");
        var itens = new List<ItemFaturaMobile>();
        if (itensJson.ValueKind == JsonValueKind.Array)
            foreach (var item in itensJson.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object)
                    itens.Add(new ItemFaturaMobile(
                        Texto(item, "descricao", "nome", "tipo"),
                        Valor(item, "valor", "valorTotal")));
        return (new FaturaDetalheMobile(fatura, itens), "Consulta concluída.");
    }

    public Task<(bool Ok, string Mensagem)> QuitarFaturaAsync(
        long id, CancellationToken ct = default) => id <= 0
        ? Task.FromResult((false, "Fatura inválida."))
        : EnviarMudancaAsync(HttpMethod.Post, $"{Faturas}/{id}/quitar", new { }, ct);

    public async Task<(PreviaLocacaoMobile? Previa, string Mensagem)> PreviaLocacaoAsync(
        long reservaId, CancellationToken ct = default)
    {
        if (reservaId <= 0) return (null, "Reserva inválida.");
        var (elemento, erro) = await ObterJsonAsync(
            $"api/reservas/{reservaId}/cobranca", ct);
        if (elemento is null) return (null, erro);
        var raiz = elemento.Value;
        var bloco = Propriedade(raiz, "previaTransferencia", "previa", "cobranca");
        if (bloco.ValueKind != JsonValueKind.Object) bloco = raiz;
        var principalPresente = Propriedade(bloco,
            "valorOriginal", "principal", "valorPrincipal").ValueKind is
            JsonValueKind.Number or JsonValueKind.String;
        var competencia = Texto(bloco, "competenciaDestino", "competencia",
            "proximaCompetencia");
        var vencimento = Texto(bloco, "vencimentoDestino", "vencimento");
        var status = Texto(raiz, "status", "situacao");
        var acrescimoJson = Propriedade(bloco, "valorAcrescimo", "acrescimo");
        // Somente permite a modalidade de transferência quando há prévia suficiente.
        var previaValida = principalPresente &&
            (acrescimoJson.ValueKind is JsonValueKind.Number or JsonValueKind.String) &&
            !string.IsNullOrWhiteSpace(competencia) && !string.IsNullOrWhiteSpace(vencimento);
        return (new PreviaLocacaoMobile(reservaId, status,
            Valor(bloco, "valorOriginal", "principal", "valorPrincipal"),
            Valor(bloco, "valorAcrescimo", "acrescimo"), competencia,
            vencimento, previaValida), "Consulta concluída.");
    }

    public Task<(bool Ok, string Mensagem)> DecidirLocacaoAsync(long reservaId,
        bool transferir, string competenciaDaPrevia, CancellationToken ct = default)
    {
        if (reservaId <= 0) return Task.FromResult((false, "Reserva inválida."));
        if (transferir && string.IsNullOrWhiteSpace(competenciaDaPrevia))
            return Task.FromResult((false, "A competência da transferência deve vir da API."));
        return EnviarMudancaAsync(HttpMethod.Post,
            $"api/reservas/{reservaId}/cobranca/decidir",
            new { modalidade = transferir ? "TransferirMensalidade" : "QuitarSimulada",
                competenciaDestino = transferir ? competenciaDaPrevia : null }, ct);
    }

    public Task<(List<TurmaMobile>? Itens, string Mensagem)> TurmasAsync(
        CancellationToken ct = default) => ListarAsync([Turmas], LerTurma, ct);

    public async Task<(VagasTurmaMobile? Vagas, string Mensagem)> VagasTurmaAsync(
        long id, CancellationToken ct = default)
    {
        if (id <= 0) return (null, "Turma inválida.");
        var (json, erro) = await ObterJsonAsync($"{Turmas}/{id}/vagas", ct);
        if (json is null) return (null, erro);
        var item = json.Value;
        return (new VagasTurmaMobile((int)Inteiro(item, "confirmadas"),
            (int)Inteiro(item, "emEspera"), (int)Inteiro(item, "vagasDisponiveis")),
            "Vagas consultadas na API.");
    }

    public Task<(List<EventoMobile>? Itens, string Mensagem)> EventosAsync(
        CancellationToken ct = default) => ListarAsync([Eventos], LerEvento, ct);

    public Task<(List<InscricaoMobile>? Itens, string Mensagem)> MinhasTurmasAsync(
        CancellationToken ct = default) => ListarAsync(
            [$"{Turmas}/minhas-inscricoes"], LerMinhaTurma, ct);

    public Task<(List<InscricaoMobile>? Itens, string Mensagem)> MeusEventosAsync(
        CancellationToken ct = default) => ListarAsync(
            [$"{Eventos}/minhas-inscricoes"], LerMeuEvento, ct);

    public Task<(bool Ok, string Mensagem)> InscreverTurmaAsync(
        long turmaId, CancellationToken ct = default) =>
        EnviarMudancaAsync(HttpMethod.Post, $"{Turmas}/{turmaId}/inscricoes", null, ct);

    public Task<(bool Ok, string Mensagem)> CancelarInscricaoTurmaAsync(
        long turmaId, CancellationToken ct = default) =>
        EnviarMudancaAsync(HttpMethod.Delete, $"{Turmas}/{turmaId}/inscricoes/minha", null, ct);

    public Task<(bool Ok, string Mensagem)> InscreverEventoAsync(
        long eventoId, CancellationToken ct = default) =>
        EnviarMudancaAsync(HttpMethod.Post, $"{Eventos}/{eventoId}/inscricoes", null, ct);

    public Task<(bool Ok, string Mensagem)> CancelarInscricaoEventoAsync(
        long eventoId, CancellationToken ct = default) =>
        EnviarMudancaAsync(HttpMethod.Delete, $"{Eventos}/{eventoId}/inscricoes/minha", null, ct);

    private async Task<(JsonElement? Elemento, string Mensagem)> ObterJsonAsync(
        string caminho, CancellationToken ct)
    {
        try
        {
            var token = await _sessao.LerTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) return (null, "Sessão ausente. Entre novamente.");
            using var pedido = new HttpRequestMessage(HttpMethod.Get, caminho);
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resposta = await _http.SendAsync(pedido, ct);
            if (!resposta.IsSuccessStatusCode)
                return (null, await MensagemErroAsync(resposta, ct));
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(ct));
            return (doc.RootElement.Clone(), "Consulta concluída.");
        }
        catch (HttpRequestException) { return (null, "API indisponível. Esta operação exige rede."); }
        catch (TaskCanceledException) { return (null, "Tempo limite de comunicação com a API."); }
        catch (JsonException) { return (null, "A API retornou dados inesperados."); }
        catch (Exception) { return (null, "Não foi possível consultar a API."); }
    }

    private async Task<(bool Ok, string Mensagem)> EnviarMudancaAsync(
        HttpMethod metodo, string caminho, object? corpo, CancellationToken ct)
    {
        try
        {
            var token = await _sessao.LerTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) return (false, "Sessão ausente. Entre novamente.");
            using var pedido = new HttpRequestMessage(metodo, caminho);
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (corpo is not null) pedido.Content = JsonContent.Create(corpo);
            using var resposta = await _http.SendAsync(pedido, ct);
            return resposta.IsSuccessStatusCode
                ? (true, "Operação confirmada pela API.")
                : (false, await MensagemErroAsync(resposta, ct));
        }
        catch (HttpRequestException) { return (false, "API indisponível. Nenhuma confirmação local foi feita."); }
        catch (TaskCanceledException) { return (false, "Resposta incerta. Consulte o histórico antes de repetir."); }
        catch (Exception) { return (false, "Falha de comunicação. Consulte o histórico antes de repetir."); }
    }

    private static FaturaMobile? LerFatura(JsonElement item)
    {
        var id = Inteiro(item, "id", "faturaId");
        return id <= 0 ? null : new FaturaMobile(id,
            Texto(item, "competencia", "mesCompetencia"),
            Texto(item, "dataVencimento", "vencimento"),
            Texto(item, "status", "situacao"),
            Valor(item, "valorTotal", "total", "valor"),
            Valor(item, "multaPrevista", "valorMultaPrevista"));
    }

    private static TurmaMobile? LerTurma(JsonElement item)
    {
        var id = Inteiro(item, "id", "turmaId");
        return id <= 0 ? null : new TurmaMobile(id,
            Texto(item, "atividade"), Texto(item, "tipo"), Texto(item, "clube"),
            Horario(item, "inicioLocal"), Horario(item, "fimLocal"),
            (int)Inteiro(item, "capacidade"), Texto(item, "status"));
    }

    private static EventoMobile? LerEvento(JsonElement item)
    {
        var id = Inteiro(item, "id", "eventoId");
        var vagas = Propriedade(item, "vagasDisponiveis");
        int? disponiveis = vagas.ValueKind == JsonValueKind.Number
            ? (int?)Inteiro(item, "vagasDisponiveis") : null;
        return id <= 0 ? null : new EventoMobile(id,
            Texto(item, "nome"), Texto(item, "tipo"), Texto(item, "clube"),
            Horario(item, "inicioLocal"), Horario(item, "fimLocal"),
            Texto(item, "publicoAlvo"), Texto(item, "status"), disponiveis);
    }

    private static InscricaoMobile? LerMinhaTurma(JsonElement item)
    {
        var id = Inteiro(item, "id", "inscricaoId");
        return id <= 0 ? null : new InscricaoMobile(id,
            Inteiro(item, "turmaId"), Texto(item, "atividade"),
            Texto(item, "clube"), Horario(item, "inicioLocal"),
            Texto(item, "status"),
            Propriedade(item, "posicaoEspera").ValueKind == JsonValueKind.Number
                ? (int?)Inteiro(item, "posicaoEspera") : null, true);
    }

    private static InscricaoMobile? LerMeuEvento(JsonElement item)
    {
        var id = Inteiro(item, "id", "inscricaoId");
        return id <= 0 ? null : new InscricaoMobile(id,
            Inteiro(item, "eventoId"), Texto(item, "evento"),
            Texto(item, "clube"), Horario(item, "inicioLocal"),
            Texto(item, "status"), null, false);
    }
}
