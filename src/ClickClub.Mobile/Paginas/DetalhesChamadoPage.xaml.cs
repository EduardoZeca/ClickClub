using ClickClub.Mobile.Servicos;
using Microsoft.Maui.Graphics;

namespace ClickClub.Mobile.Paginas;

public partial class DetalhesChamadoPage : ContentPage
{
    private readonly ApiClickClub _api;
    private readonly long _id;
    private bool _ocupado;

    public DetalhesChamadoPage(ApiClickClub api, long id)
    {
        InitializeComponent();
        _api = api;
        _id = id;
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
        Indicador.IsVisible = Indicador.IsRunning = true;
        Respostas.Children.Clear();
        try
        {
            var (item, erro) = await _api.DetalhesChamadoAsync(_id);
            Mensagem.Text = item is null ? erro : "Histórico consultado na API.";
            if (item is null) return;
            Assunto.Text = $"#{item.Chamado.Id} — {item.Chamado.Assunto}";
            Resumo.Text = $"{item.Chamado.Tipo} · {item.Chamado.Categoria} · {item.Chamado.Status} · {item.Chamado.Clube}";
            Descricao.Text = item.Chamado.Descricao;
            if (item.Respostas.Count == 0)
            {
                Respostas.Children.Add(new Label { Text = "Ainda não há respostas registradas.",
                    TextColor = Color.FromArgb("#536477") });
                return;
            }
            foreach (var resposta in item.Respostas)
            {
                var painel = new VerticalStackLayout { Spacing = 6 };
                painel.Children.Add(new Label { Text = $"{resposta.Autor} · {resposta.DataHora}",
                    FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#153559") });
                painel.Children.Add(new Label { Text = resposta.Conteudo,
                    TextColor = Color.FromArgb("#25384B") });
                Respostas.Children.Add(new Border { Padding = new Thickness(14),
                    BackgroundColor = Colors.White,
                    Stroke = new SolidColorBrush(Color.FromArgb("#DFE5EF")), Content = painel });
            }
        }
        finally
        {
            _ocupado = false;
            Indicador.IsVisible = Indicador.IsRunning = false;
        }
    }
}
