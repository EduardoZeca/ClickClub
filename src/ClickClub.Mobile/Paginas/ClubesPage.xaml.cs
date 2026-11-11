using Microsoft.Maui.Graphics;
using ClickClub.Mobile.Modelos;
using ClickClub.Mobile.Servicos;

namespace ClickClub.Mobile.Paginas;

public partial class ClubesPage : ContentPage
{
    private readonly ApiClickClub _api;
    private bool _carregou;
    private bool _carregando;

    public ClubesPage(ApiClickClub api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_carregou)
        {
            _carregou = true;
            await CarregarAsync();
        }
    }

    private async void AoAtualizar(object sender, EventArgs e) => await CarregarAsync();

    private async Task CarregarAsync()
    {
        if (_carregando) return;
        _carregando = true;
        BotaoAtualizar.IsEnabled = false;
        Indicador.IsVisible = Indicador.IsRunning = true;
        ListaClubes.Children.Clear();
        try
        {
            var (clubes, mensagem) = await _api.ClubesAsync();
            Mensagem.Text = clubes is null ? mensagem : clubes.Count == 0
                ? "Nenhum clube disponível." : $"{clubes.Count} clube(s) encontrado(s).";
            if (clubes is null) return;
            foreach (var clube in clubes)
            {
                var selecionado = clube;
                var painel = new VerticalStackLayout { Spacing = 8 };
                painel.Children.Add(new Label
                {
                    Text = selecionado.Nome,
                    FontSize = 20, FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#153559")
                });
                painel.Children.Add(new Label
                {
                    Text = $"{selecionado.Municipio} {selecionado.Uf}".Trim(),
                    TextColor = Color.FromArgb("#536477")
                });
                var botao = new Button { Text = "Ver espaços" };
                botao.Clicked += async (_, _) =>
                    await Navigation.PushAsync(new EspacosPage(_api, selecionado));
                painel.Children.Add(botao);
                ListaClubes.Children.Add(new Border
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
