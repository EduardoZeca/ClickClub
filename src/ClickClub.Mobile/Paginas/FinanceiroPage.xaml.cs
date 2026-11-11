using ClickClub.Mobile.Servicos;
using Microsoft.Maui.Graphics;

namespace ClickClub.Mobile.Paginas;

public partial class FinanceiroPage : ContentPage
{
    private readonly ApiClickClub _api;
    private bool _ocupado;

    public FinanceiroPage(ApiClickClub api)
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
            var (itens, erro) = await _api.MinhasFaturasAsync();
            Mensagem.Text = itens is null ? erro : itens.Count == 0
                ? "Nenhuma fatura encontrada." : $"{itens.Count} fatura(s).";
            if (itens is null) return;
            foreach (var item in itens)
            {
                var painel = new VerticalStackLayout { Spacing = 8 };
                painel.Children.Add(new Label
                {
                    Text = $"Fatura #{item.Id}", FontAttributes = FontAttributes.Bold,
                    FontSize = 18, TextColor = Color.FromArgb("#153559")
                });
                painel.Children.Add(new Label { Text = item.Resumo,
                    TextColor = Color.FromArgb("#536477") });
                var botao = new Button { Text = "Consultar detalhes" };
                botao.Clicked += async (_, _) =>
                    await Navigation.PushAsync(new FaturaDetalhesPage(_api, item.Id));
                painel.Children.Add(botao);
                Lista.Children.Add(new Border { BackgroundColor = Colors.White,
                    Stroke = new SolidColorBrush(Color.FromArgb("#DFE5EF")),
                    Padding = new Thickness(16), Content = painel });
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
