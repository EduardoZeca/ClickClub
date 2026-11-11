using ClickClub.Mobile.Modelos;
using ClickClub.Mobile.Servicos;
using Microsoft.Maui.Graphics;

namespace ClickClub.Mobile.Paginas;

public partial class MinhasInscricoesPage : ContentPage
{
    private readonly ApiClickClub _api;
    private bool _ocupado;

    public MinhasInscricoesPage(ApiClickClub api)
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
        ListaTurmas.Children.Clear();
        ListaEventos.Children.Clear();
        Atualizar.IsEnabled = false;
        Indicador.IsVisible = Indicador.IsRunning = true;
        try
        {
            var (turmas, erroTurmas) = await _api.MinhasTurmasAsync();
            var (eventos, erroEventos) = await _api.MeusEventosAsync();
            Mensagem.Text = turmas is null || eventos is null
                ? $"Turmas: {erroTurmas} Eventos: {erroEventos}"
                : "Histórico consultado na API.";
            if (turmas is not null)
                foreach (var item in turmas) ListaTurmas.Children.Add(CriarLinha(item));
            if (eventos is not null)
                foreach (var item in eventos) ListaEventos.Children.Add(CriarLinha(item));
        }
        finally
        {
            _ocupado = false;
            Indicador.IsVisible = Indicador.IsRunning = false;
            Atualizar.IsEnabled = true;
        }
    }

    private Border CriarLinha(InscricaoMobile item)
    {
        var painel = new VerticalStackLayout { Spacing = 8 };
        painel.Children.Add(new Label { Text = item.Resumo, FontSize = 16,
            TextColor = Color.FromArgb("#153559") });
        if (item.Status.Equals("Confirmada", StringComparison.OrdinalIgnoreCase) ||
            item.Status.Equals("EmEspera", StringComparison.OrdinalIgnoreCase) ||
            item.Status.Equals("Inscrito", StringComparison.OrdinalIgnoreCase))
        {
            var botao = new Button { Text = "Cancelar minha inscrição",
                BackgroundColor = Color.FromArgb("#895C36") };
            botao.Clicked += async (_, _) => await CancelarAsync(item);
            painel.Children.Add(botao);
        }
        return new Border { BackgroundColor = Colors.White,
            Stroke = new SolidColorBrush(Color.FromArgb("#DFE5EF")),
            Padding = new Thickness(16), Content = painel };
    }

    private async Task CancelarAsync(InscricaoMobile item)
    {
        if (_ocupado) return;
        if (!await DisplayAlertAsync("Cancelar inscrição",
            $"Cancelar inscrição em {item.Nome}? A API aplicará a regra de vagas e espera.",
            "Cancelar inscrição", "Voltar")) return;
        _ocupado = true;
        try
        {
            var (ok, mensagem) = item.EhTurma
                ? await _api.CancelarInscricaoTurmaAsync(item.ReferenciaId)
                : await _api.CancelarInscricaoEventoAsync(item.ReferenciaId);
            Mensagem.Text = mensagem;
            if (ok) await DisplayAlertAsync("Inscrição", "Cancelamento confirmado na API.", "OK");
        }
        finally { _ocupado = false; }
        await CarregarAsync();
    }
}
