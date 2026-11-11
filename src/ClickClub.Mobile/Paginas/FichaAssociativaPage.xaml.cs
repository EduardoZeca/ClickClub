using ClickClub.Mobile.Servicos;
using Microsoft.Maui.Graphics;

namespace ClickClub.Mobile.Paginas;

public partial class FichaAssociativaPage : ContentPage
{
    private readonly ApiClickClub _api;
    private bool _ocupado;

    public FichaAssociativaPage(ApiClickClub api)
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
        Indicador.IsVisible = Indicador.IsRunning = true;
        Campos.Children.Clear();
        try
        {
            var (ficha, erro) = await _api.MinhaFichaAsync();
            Mensagem.Text = ficha is null ? erro : "Ficha consultada na API.";
            if (ficha is null) return;
            Adicionar("Matrícula", ficha.Matricula);
            Adicionar("Situação", ficha.Situacao);
            Adicionar("Titular", ficha.Titular);
            Adicionar("Categoria", ficha.Categoria);
            Adicionar("Tipo de vínculo", ficha.TipoVinculo);
            Adicionar("Clube de origem", ficha.Clube);
            Adicionar("Início", ficha.DataInicio);
            Adicionar("Dependentes", ficha.Dependentes.Count == 0 ?
                "Nenhum registrado nesta consulta." : string.Join(", ", ficha.Dependentes));
        }
        finally
        {
            _ocupado = false;
            Indicador.IsVisible = Indicador.IsRunning = false;
        }
    }

    private void Adicionar(string nome, string valor)
    {
        var painel = new VerticalStackLayout { Spacing = 4 };
        painel.Children.Add(new Label { Text = nome, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#153559") });
        painel.Children.Add(new Label { Text = string.IsNullOrWhiteSpace(valor) ? "Não informado" : valor,
            TextColor = Color.FromArgb("#25384B") });
        Campos.Children.Add(new Border { Padding = new Thickness(12),
            BackgroundColor = Colors.White,
            Stroke = new SolidColorBrush(Color.FromArgb("#DFE5EF")), Content = painel });
    }
}
