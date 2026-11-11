using ClickClub.Mobile.Modelos;
using ClickClub.Mobile.Servicos;
using Microsoft.Maui.Graphics;

namespace ClickClub.Mobile.Paginas;

public partial class FaturaDetalhesPage : ContentPage
{
    private readonly ApiClickClub _api;
    private readonly long _id;
    private bool _ocupado;
    private FaturaDetalheMobile? _detalhe;

    public FaturaDetalhesPage(ApiClickClub api, long id)
    {
        InitializeComponent();
        _api = api;
        _id = id;
        Titulo.Text = $"Fatura #{id}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        if (_ocupado) return;
        _ocupado = true;
        Indicador.IsVisible = Indicador.IsRunning = true;
        BotaoQuitar.IsVisible = false;
        ListaItens.Children.Clear();
        try
        {
            var (detalhe, mensagem) = await _api.DetalharFaturaAsync(_id);
            _detalhe = detalhe;
            Mensagem.Text = mensagem;
            Resumo.Text = detalhe?.Cabecalho.Resumo ?? "";
            if (detalhe is null) return;
            foreach (var item in detalhe.Itens)
                ListaItens.Children.Add(new Label { Text = item.Resumo,
                    TextColor = Color.FromArgb("#536477") });
            if (detalhe.Itens.Count == 0)
                ListaItens.Children.Add(new Label { Text =
                    "A API não forneceu itens nesta resposta. Consulte a Web para conferir detalhes." });
            BotaoQuitar.IsVisible = detalhe.Cabecalho.EmAberto;
        }
        finally
        {
            _ocupado = false;
            Indicador.IsVisible = Indicador.IsRunning = false;
        }
    }

    private async void AoQuitar(object sender, EventArgs e)
    {
        if (_ocupado || _detalhe?.Cabecalho.EmAberto != true) return;
        var confirmou = await DisplayAlertAsync("Quitação simulada",
            "Registrar a quitação integral simulada desta fatura? A API poderá aplicar multa de atraso.",
            "Confirmar", "Cancelar");
        if (!confirmou) return;
        _ocupado = true;
        BotaoQuitar.IsEnabled = false;
        try
        {
            var (ok, mensagem) = await _api.QuitarFaturaAsync(_id);
            Mensagem.Text = mensagem;
            if (!ok) return;
            await DisplayAlertAsync("Financeiro", "Operação confirmada pela API.", "OK");
        }
        finally
        {
            _ocupado = false;
            BotaoQuitar.IsEnabled = true;
        }
        await CarregarAsync();
    }
}
