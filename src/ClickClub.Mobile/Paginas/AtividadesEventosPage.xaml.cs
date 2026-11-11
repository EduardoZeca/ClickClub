using ClickClub.Mobile.Modelos;
using ClickClub.Mobile.Servicos;
using Microsoft.Maui.Graphics;

namespace ClickClub.Mobile.Paginas;

public partial class AtividadesEventosPage : ContentPage
{
    private readonly ApiClickClub _api;
    private bool _ocupado;

    public AtividadesEventosPage(ApiClickClub api)
    {
        InitializeComponent();
        _api = api;
        Tipo.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarAsync();
    }

    private async void AoTrocarTipo(object sender, EventArgs e) => await CarregarAsync();
    private async void AoAtualizar(object sender, EventArgs e) => await CarregarAsync();
    private async void AoMinhas(object sender, EventArgs e) =>
        await Navigation.PushAsync(new MinhasInscricoesPage(_api));

    private async Task CarregarAsync()
    {
        if (_ocupado) return;
        _ocupado = true;
        Atualizar.IsEnabled = false;
        Lista.Children.Clear();
        Indicador.IsVisible = Indicador.IsRunning = true;
        try
        {
            if (Tipo.SelectedIndex == 1)
            {
                var (eventos, erro) = await _api.EventosAsync();
                Mensagem.Text = eventos is null ? erro : $"{eventos.Count} evento(s) encontrado(s).";
                if (eventos is null) return;
                foreach (var evento in eventos)
                {
                    var item = evento;
                    var painel = NovoPainel(item.Resumo);
                    if (item.Status.Equals("Publicado", StringComparison.OrdinalIgnoreCase))
                    {
                        var botao = new Button { Text = "Solicitar inscrição" };
                        botao.Clicked += async (_, _) => await InscreverAsync(item.Id, false);
                        painel.Children.Add(botao);
                    }
                    Lista.Children.Add(Emoldurar(painel));
                }
            }
            else
            {
                var (turmas, erro) = await _api.TurmasAsync();
                Mensagem.Text = turmas is null ? erro : $"{turmas.Count} turma(s) encontrada(s).";
                if (turmas is null) return;
                foreach (var turma in turmas)
                {
                    var item = turma;
                    var painel = NovoPainel(item.Resumo);
                    var (vagas, _) = await _api.VagasTurmaAsync(item.Id);
                    if (vagas is not null)
                        painel.Children.Add(new Label { Text = vagas.Resumo,
                            TextColor = Color.FromArgb("#536477") });
                    if (item.Status.Equals("Aberta", StringComparison.OrdinalIgnoreCase))
                    {
                        var botao = new Button { Text = "Solicitar inscrição" };
                        botao.Clicked += async (_, _) => await InscreverAsync(item.Id, true);
                        painel.Children.Add(botao);
                    }
                    Lista.Children.Add(Emoldurar(painel));
                }
            }
        }
        finally
        {
            _ocupado = false;
            Indicador.IsVisible = Indicador.IsRunning = false;
            Atualizar.IsEnabled = true;
        }
    }

    private async Task InscreverAsync(long id, bool turma)
    {
        if (_ocupado) return;
        if (!await DisplayAlertAsync("Inscrição",
            turma ? "Solicitar inscrição nesta turma? Se estiver lotada, a API poderá registrar espera."
                  : "Solicitar inscrição neste evento?",
            "Confirmar", "Voltar")) return;
        _ocupado = true;
        try
        {
            var (ok, mensagem) = turma
                ? await _api.InscreverTurmaAsync(id)
                : await _api.InscreverEventoAsync(id);
            Mensagem.Text = mensagem;
            if (ok) await DisplayAlertAsync("Inscrição",
                "A API aceitou a solicitação. Confira a situação em Minhas inscrições.", "OK");
        }
        finally { _ocupado = false; }
        await CarregarAsync();
    }

    private static VerticalStackLayout NovoPainel(string resumo)
    {
        var painel = new VerticalStackLayout { Spacing = 8 };
        painel.Children.Add(new Label { Text = resumo, FontSize = 16,
            TextColor = Color.FromArgb("#153559") });
        return painel;
    }

    private static Border Emoldurar(VerticalStackLayout painel) => new()
    {
        BackgroundColor = Colors.White,
        Stroke = new SolidColorBrush(Color.FromArgb("#DFE5EF")),
        Padding = new Thickness(16), Content = painel
    };
}
