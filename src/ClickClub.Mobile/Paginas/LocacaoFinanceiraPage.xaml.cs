using ClickClub.Mobile.Modelos;
using ClickClub.Mobile.Servicos;

namespace ClickClub.Mobile.Paginas;

public partial class LocacaoFinanceiraPage : ContentPage
{
    private readonly ApiClickClub _api;
    private readonly long _reservaId;
    private PreviaLocacaoMobile? _previa;
    private bool _ocupado;

    public LocacaoFinanceiraPage(ApiClickClub api, long reservaId)
    {
        InitializeComponent();
        _api = api;
        _reservaId = reservaId;
        Titulo.Text = $"Reserva #{reservaId}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarAsync();
    }

    private async void AoAtualizar(object sender, EventArgs e) => await CarregarAsync();

    private async Task CarregarAsync()
    {
        if (_ocupado) return;
        _ocupado = true;
        BotaoQuitar.IsEnabled = BotaoTransferir.IsEnabled = false;
        Indicador.IsVisible = Indicador.IsRunning = true;
        try
        {
            var (previa, erro) = await _api.PreviaLocacaoAsync(_reservaId);
            _previa = previa;
            Mensagem.Text = erro;
            Resumo.Text = previa?.Resumo ?? "Sem prévia disponível.";
            // Falha fechada: nenhuma decisão sem dados de valor e competência.
            var emAberto = previa?.Status.Equals("EmAberto",
                StringComparison.OrdinalIgnoreCase) == true;
            BotaoQuitar.IsEnabled = emAberto && previa!.PossuiPreviaServidor;
            BotaoTransferir.IsEnabled = emAberto && previa!.PossuiPreviaServidor;
            if (previa is not null && !previa.PossuiPreviaServidor)
                Mensagem.Text = "Prévia financeira incompleta. Não é seguro confirmar sem consultar a API/Web.";
        }
        finally
        {
            _ocupado = false;
            Indicador.IsVisible = Indicador.IsRunning = false;
        }
    }

    private async void AoQuitar(object sender, EventArgs e) => await DecidirAsync(false);
    private async void AoTransferir(object sender, EventArgs e) => await DecidirAsync(true);

    private async Task DecidirAsync(bool transferir)
    {
        if (_ocupado || _previa?.PossuiPreviaServidor != true) return;
        var texto = transferir
            ? $"Principal {FormatoMobile.Moeda(_previa.Principal)}, acréscimo " +
              $"{FormatoMobile.Moeda(_previa.Acrescimo)}, competência {_previa.Competencia}, " +
              $"vencimento {_previa.Vencimento}. Confirmar transferência simulada?"
            : $"Confirmar quitação avulsa simulada da taxa de " +
              $"{FormatoMobile.Moeda(_previa.Principal)}?";
        if (!await DisplayAlertAsync("Confirmar decisão financeira", texto,
            "Confirmar", "Voltar")) return;
        _ocupado = true;
        BotaoQuitar.IsEnabled = BotaoTransferir.IsEnabled = false;
        try
        {
            var (ok, mensagem) = await _api.DecidirLocacaoAsync(
                _reservaId, transferir, _previa.Competencia);
            Mensagem.Text = mensagem;
            if (ok) await DisplayAlertAsync("Financeiro", "Decisão confirmada na API.", "OK");
        }
        finally { _ocupado = false; }
        await CarregarAsync();
    }
}
