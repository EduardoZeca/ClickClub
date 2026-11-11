using ClickClub.Mobile.Servicos;
using Microsoft.Maui.Graphics;

namespace ClickClub.Mobile.Paginas;

public partial class MuralPage : ContentPage
{
    private readonly ApiClickClub _api;
    private bool _ocupado;

    public MuralPage(ApiClickClub api)
    {
        InitializeComponent();
        _api = api;
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
        Atualizar.IsEnabled = false;
        Indicador.IsVisible = Indicador.IsRunning = true;
        Lista.Children.Clear();
        try
        {
            var (itens, mensagem) = await _api.AvisosAsync();
            Mensagem.Text = itens is null ? mensagem : itens.Count == 0
                ? "Não há avisos publicados." : $"{itens.Count} aviso(s) vigente(s).";
            if (itens is null) return;
            foreach (var aviso in itens)
            {
                var painel = new VerticalStackLayout { Spacing = 8 };
                painel.Children.Add(new Label
                {
                    Text = aviso.Titulo, FontSize = 19,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#153559")
                });
                painel.Children.Add(new Label { Text = aviso.Clube,
                    TextColor = Color.FromArgb("#536477") });
                painel.Children.Add(new Label { Text = aviso.Corpo,
                    TextColor = Color.FromArgb("#25384B") });
                painel.Children.Add(new Label { Text = $"Período: {aviso.Inicio} até {aviso.Fim}",
                    FontSize = 12, TextColor = Color.FromArgb("#536477") });
                Lista.Children.Add(new Border
                {
                    Padding = new Thickness(16), BackgroundColor = Colors.White,
                    Stroke = new SolidColorBrush(Color.FromArgb("#DFE5EF")),
                    Content = painel
                });
            }
        }
        finally
        {
            _ocupado = false;
            Atualizar.IsEnabled = true;
            Indicador.IsVisible = Indicador.IsRunning = false;
        }
    }
}
