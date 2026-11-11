using Microsoft.Maui.Graphics;
using System.Globalization;
using ClickClub.Mobile.Modelos;
using ClickClub.Mobile.Servicos;

namespace ClickClub.Mobile.Paginas;

public partial class EspacosPage : ContentPage
{
    private readonly ApiClickClub _api;
    private readonly ClubeMobile _clube;
    private bool _carregou;
    private bool _carregando;

    public EspacosPage(ApiClickClub api, ClubeMobile clube)
    {
        InitializeComponent();
        _api = api;
        _clube = clube;
        NomeClube.Text = clube.Nome;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_carregou) return;
        _carregou = true;
        await CarregarAsync();
    }

    private async void AoAtualizar(object sender, EventArgs e) => await CarregarAsync();

    private async Task CarregarAsync()
    {
        if (_carregando) return;
        _carregando = true;
        BotaoAtualizar.IsEnabled = false;
        Indicador.IsVisible = Indicador.IsRunning = true;
        ListaEspacos.Children.Clear();
        try
        {
            var (espacos, mensagem) = await _api.EspacosAsync(_clube.Id);
            Mensagem.Text = espacos is null ? mensagem : espacos.Count == 0
                ? "Nenhum espaço ativo encontrado." : $"{espacos.Count} espaço(s) disponível(is) para consulta.";
            if (espacos is null) return;
            foreach (var espaco in espacos)
            {
                var escolhido = espaco;
                var painel = new VerticalStackLayout { Spacing = 8 };
                painel.Children.Add(new Label
                {
                    Text = escolhido.Nome, FontSize = 20,
                    FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#153559")
                });
                painel.Children.Add(new Label
                {
                    Text = $"{escolhido.Tipo} · Capacidade: {escolhido.Capacidade} · Taxa base: {escolhido.TaxaBase.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))}",
                    TextColor = Color.FromArgb("#536477")
                });
                var botao = new Button { Text = "Solicitar reserva" };
                botao.Clicked += async (_, _) =>
                    await Navigation.PushAsync(new NovaReservaPage(_api, _clube, escolhido));
                painel.Children.Add(botao);
                ListaEspacos.Children.Add(new Border
                {
                    BackgroundColor = Colors.White,
                    Stroke = new SolidColorBrush(Color.FromArgb("#DFE5EF")),
                    Padding = new Thickness(16), Content = painel
                });
            }
        }
        finally
        {
            Indicador.IsVisible = Indicador.IsRunning = false;
            BotaoAtualizar.IsEnabled = true;
            _carregando = false;
        }
    }
}
