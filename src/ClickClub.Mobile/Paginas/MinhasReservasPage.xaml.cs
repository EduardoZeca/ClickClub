using Microsoft.Maui.Graphics;
using System.Globalization;
using ClickClub.Mobile.Modelos;
using ClickClub.Mobile.Servicos;

namespace ClickClub.Mobile.Paginas;

public partial class MinhasReservasPage : ContentPage
{
    private readonly ApiClickClub _api;
    private bool _carregou;
    private bool _carregando;
    private bool _cancelando;

    public MinhasReservasPage(ApiClickClub api)
    {
        InitializeComponent();
        _api = api;
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
        if (_carregando || _cancelando) return;
        _carregando = true;
        BotaoAtualizar.IsEnabled = false;
        Indicador.IsVisible = Indicador.IsRunning = true;
        ListaReservas.Children.Clear();
        try
        {
            var (reservas, mensagem) = await _api.MinhasReservasAsync();
            Mensagem.Text = reservas is null ? mensagem : reservas.Count == 0
                ? "Nenhuma reserva encontrada." : $"{reservas.Count} reserva(s) no histórico.";
            if (reservas is null) return;
            foreach (var reserva in reservas)
            {
                var item = reserva;
                var painel = new VerticalStackLayout { Spacing = 6 };
                painel.Children.Add(new Label
                {
                    Text = $"Reserva #{item.Id} · {item.Espaco}",
                    FontSize = 19, FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#153559")
                });
                painel.Children.Add(new Label
                {
                    Text = $"Clube: {item.Clube}\nInício: {item.Inicio}\nFim: {item.Fim}\nSituação: {item.Situacao}\nValor: {item.Valor.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))}",
                    TextColor = Color.FromArgb("#536477")
                });
                if (item.Situacao.Equals("Confirmada", StringComparison.OrdinalIgnoreCase) && item.Valor > 0)
                {
                    var financeiro = new Button { Text = "Consultar cobrança simulada" };
                    financeiro.Clicked += async (_, _) =>
                        await Navigation.PushAsync(new LocacaoFinanceiraPage(_api, item.Id));
                    painel.Children.Add(financeiro);
                }
                if (item.Situacao.Equals("Confirmada", StringComparison.OrdinalIgnoreCase))
                {
                    var botao = new Button
                    {
                        Text = "Solicitar cancelamento",
                        BackgroundColor = Color.FromArgb("#895C36")
                    };
                    botao.Clicked += async (_, _) => await CancelarAsync(item.Id);
                    painel.Children.Add(botao);
                }
                ListaReservas.Children.Add(new Border
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

    private async Task CancelarAsync(long id)
    {
        if (_cancelando || _carregando) return;
        var confirmou = await DisplayAlertAsync("Cancelar reserva",
            $"Solicitar cancelamento da reserva #{id}? A API aplicará os prazos, multas e créditos simulados.",
            "Solicitar", "Voltar");
        if (!confirmou) return;
        _cancelando = true;
        BotaoAtualizar.IsEnabled = false;
        try
        {
            var (ok, mensagem) = await _api.CancelarReservaAsync(id);
            Mensagem.Text = mensagem;
            if (ok)
            {
                await DisplayAlertAsync("Cancelamento",
                    "A API confirmou a operação. O histórico será atualizado.", "OK");
            }
        }
        finally
        {
            _cancelando = false;
            BotaoAtualizar.IsEnabled = true;
        }
        await CarregarAsync();
    }
}
