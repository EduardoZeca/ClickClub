using System.Net;
using System.Text.Json;
using ClickClub.Mobile.Modelos;

namespace ClickClub.Mobile.Servicos;

// C7: contratos conferidos em AvisosController / OuvidoriaController.
// C2: a ficha é consulta exploratória SOMENTE GET; não inventar POST de adesão.
public sealed partial class ApiClickClub
{
    public Task<(List<AvisoMobile>? Itens, string Mensagem)> AvisosAsync(
        long? unidadeId = null, CancellationToken ct = default)
    {
        var rota = unidadeId is > 0 ? $"api/avisos?unidadeId={unidadeId}" : "api/avisos";
        return ListarAsync([rota], LerAvisoMobile, ct);
    }

    public Task<(List<ClubeOuvidoriaMobile>? Itens, string Mensagem)> ClubesOuvidoriaAsync(
        CancellationToken ct = default) =>
        ListarAsync(["api/ouvidoria/clubes"], LerClubeOuvidoria, ct);

    public Task<(List<ChamadoOuvidoriaMobile>? Itens, string Mensagem)> MeusChamadosAsync(
        CancellationToken ct = default) =>
        ListarAsync(["api/ouvidoria/minhas"], LerChamadoOuvidoria, ct);

    public async Task<(ChamadoOuvidoriaDetalheMobile? Item, string Mensagem)> DetalhesChamadoAsync(
        long id, CancellationToken ct = default)
    {
        if (id <= 0) return (null, "Chamado inválido.");
        var (json, erro) = await ObterJsonAsync($"api/ouvidoria/{id}", ct);
        if (json is null) return (null, erro);
        var raiz = json.Value;
        var objetoChamado = Propriedade(raiz, "chamado");
        if (objetoChamado.ValueKind != JsonValueKind.Object)
            return (null, "Resposta de ouvidoria incompatível com a API.");
        var chamado = LerChamadoOuvidoria(objetoChamado);
        if (chamado is null) return (null, "Chamado não encontrado.");
        var respostas = new List<RespostaOuvidoriaMobile>();
        var dadosRespostas = Propriedade(raiz, "respostas");
        if (dadosRespostas.ValueKind == JsonValueKind.Array)
            foreach (var resposta in dadosRespostas.EnumerateArray())
                if (resposta.ValueKind == JsonValueKind.Object)
                    respostas.Add(new RespostaOuvidoriaMobile(
                        Texto(resposta, "autor"), Texto(resposta, "conteudo"),
                        Horario(resposta, "dataHoraUtc")));
        return (new ChamadoOuvidoriaDetalheMobile(chamado, respostas), "Consulta concluída.");
    }

    public Task<(bool Ok, string Mensagem)> CriarChamadoAsync(
        long? unidadeId, string tipo, string categoria, string assunto,
        string descricao, CancellationToken ct = default)
    {
        if (unidadeId is <= 0 ||
            tipo is not ("Solicitacao" or "Reclamacao") ||
            categoria.Trim().Length is < 2 or > 80 ||
            assunto.Trim().Length is < 2 or > 150 ||
            descricao.Trim().Length is < 2 or > 4000)
            return Task.FromResult((false, "Revise clube, tipo, categoria, assunto e descrição."));
        return EnviarMudancaAsync(HttpMethod.Post, "api/ouvidoria", new
        {
            unidadeId, tipo, categoria = categoria.Trim(),
            assunto = assunto.Trim(), descricao = descricao.Trim()
        }, ct);
    }

    // A consulta de ficha usa somente GET. Os caminhos de C2 precisam ser
    // conferidos no Controller real antes da homologação. Sem gravações locais.
    public async Task<(FichaMobile? Ficha, string Mensagem)> MinhaFichaAsync(
        CancellationToken ct = default)
    {
        foreach (var rota in new[] { "api/associacoes/minha", "api/associacao/minha" })
        {
            var (json, mensagem) = await ObterJsonAsync(rota, ct);
            if (json is null)
            {
                if (mensagem.Contains("404", StringComparison.Ordinal)) continue;
                return (null, mensagem);
            }
            var raiz = json.Value;
            var obj = Propriedade(raiz, "associacao", "ficha");
            if (obj.ValueKind != JsonValueKind.Object) obj = raiz;
            if (obj.ValueKind != JsonValueKind.Object)
                return (null, "A API retornou um formato de ficha não reconhecido.");
            var matricula = Texto(obj, "matricula", "numeroMatricula");
            var situacao = Texto(obj, "status", "situacao");
            var titular = Texto(obj, "titular", "titularNome", "nomeTitular");
            var categoria = Texto(obj, "categoria", "categoriaNome");
            var vinculo = Texto(obj, "tipoVinculo", "vinculo", "tipoVinculoNome");
            var clube = Texto(obj, "clubeOrigem", "unidadeOrigem", "unidadeNome");
            var inicio = Horario(obj, "dataInicio", "inicioEmUtc");
            var dependentes = new List<string>();
            var lista = Propriedade(obj, "dependentes");
            if (lista.ValueKind == JsonValueKind.Array)
                foreach (var dep in lista.EnumerateArray())
                {
                    var nome = dep.ValueKind == JsonValueKind.String
                        ? dep.GetString() ?? "" : dep.ValueKind == JsonValueKind.Object
                        ? Texto(dep, "nome", "pessoaNome") : "";
                    if (!string.IsNullOrWhiteSpace(nome)) dependentes.Add(nome);
                }
            if (string.IsNullOrWhiteSpace(matricula) &&
                string.IsNullOrWhiteSpace(situacao) &&
                string.IsNullOrWhiteSpace(titular))
                return (null, "A resposta da API não contém os campos de ficha reconhecidos.");
            return (new FichaMobile(matricula, situacao, titular, categoria,
                vinculo, clube, inicio, dependentes), "Consulta concluída.");
        }
        return (null, "A ficha ainda não está disponível na API. Confirmar endpoint de C2.");
    }

    private static AvisoMobile? LerAvisoMobile(JsonElement item)
    {
        var id = Inteiro(item, "id");
        return id <= 0 ? null : new AvisoMobile(id,
            Texto(item, "titulo"), Texto(item, "corpo"),
            Texto(item, "clube") is { Length: > 0 } clube ? clube : "Mural geral",
            Horario(item, "inicioUtc"), Horario(item, "fimUtc"));
    }

    private static ClubeOuvidoriaMobile? LerClubeOuvidoria(JsonElement item)
    {
        var id = Inteiro(item, "id");
        return id <= 0 ? null : new ClubeOuvidoriaMobile(id, Texto(item, "nome"));
    }

    private static ChamadoOuvidoriaMobile? LerChamadoOuvidoria(JsonElement item)
    {
        var id = Inteiro(item, "id");
        return id <= 0 ? null : new ChamadoOuvidoriaMobile(id,
            Texto(item, "tipo"), Texto(item, "categoria"), Texto(item, "assunto"),
            Texto(item, "descricao"), Texto(item, "clube"), Texto(item, "status"),
            Horario(item, "criadoEmUtc"));
    }
}
