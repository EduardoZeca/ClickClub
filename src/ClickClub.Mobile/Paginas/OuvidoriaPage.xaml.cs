using ClickClub.Mobile.Servicos;
using Microsoft.Maui.Graphics;

namespace ClickClub.Mobile.Paginas;

public partial class OuvidoriaPage : ContentPage
{
    private readonly ApiClickClub _api;
    private bool _ocupado;

    public OuvidoriaPage(ApiClickClub api)
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
    private async void AoCriar(object sender, EventArgs e) =>
        await Navigation.PushAsync(new NovoChamadoPage(_api));

    private async Task CarregarAsync()
    {
        if (_ocupado) return;
        _ocupado = true;
        Atualizar.IsEnabled = false;
        Indicador.IsVisible = Indicador.IsRunning = true;
        Lista.Children.Clear();
        try
        {
            var (itens, erro) = await _api.MeusChamadosAsync();
            Mensagem.Text = itens is null ? erro : itens.Count == 0
                ? "Nenhum chamado registrado." : $"{itens.Count} chamado(s).";
            if (itens is null) return;
            foreach (var chamado in itens)
            {
                var selecionado = chamado;
                var painel = new VerticalStackLayout { Spacing = 8 };
                painel.Children.Add(new Label { Text = $"#{selecionado.Id} — {selecionado.Assunto}",
                    FontAttributes = FontAttributes.Bold, FontSize = 17,
                    TextColor = Color.FromArgb("#153559") });
                painel.Children.Add(new Label
                {
                    Text = $"{selecionado.Tipo} · {selecionado.Status} · {selecionado.Clube}",
                    TextColor = Color.FromArgb("#536477")
                });
                var botao = new Button { Text = "Detalhes e respostas" };
                botao.Clicked += async (_, _) =>
                    await Navigation.PushAsync(new DetalhesChamadoPage(_api, selecionado.Id));
                painel.Children.Add(botao);
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
