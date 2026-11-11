using ClickClub.Mobile.Modelos;
using ClickClub.Mobile.Servicos;

namespace ClickClub.Mobile.Paginas;

public partial class NovoChamadoPage : ContentPage
{
    private readonly ApiClickClub _api;
    private readonly List<ClubeOuvidoriaMobile> _clubes = [];
    private bool _ocupado;

    public NovoChamadoPage(ApiClickClub api)
    {
        InitializeComponent();
        _api = api;
        Tipo.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_clubes.Count != 0) return;
        var (clubes, erro) = await _api.ClubesOuvidoriaAsync();
        if (clubes is null) { Mensagem.Text = erro; return; }
        _clubes.Clear();
        _clubes.AddRange(clubes);
        Clube.Items.Clear();
        foreach (var clube in _clubes) Clube.Items.Add(clube.Nome);
        if (Clube.Items.Count > 0) Clube.SelectedIndex = 0;
    }

    private async void AoEnviar(object sender, EventArgs e)
    {
        if (_ocupado) return;
        if (Clube.SelectedIndex < 0 || Clube.SelectedIndex >= _clubes.Count)
        {
            Mensagem.Text = "Selecione um clube.";
            return;
        }
        var categoria = Categoria.Text?.Trim() ?? "";
        var assunto = Assunto.Text?.Trim() ?? "";
        var descricao = Descricao.Text?.Trim() ?? "";
        var tipo = Tipo.SelectedIndex == 1 ? "Reclamacao" : "Solicitacao";
        if (categoria.Length is < 2 or > 80 || assunto.Length is < 2 or > 150 ||
            descricao.Length is < 2 or > 4000)
        {
            Mensagem.Text = "Categoria, assunto e descrição devem ter ao menos 2 caracteres.";
            return;
        }
        if (!await DisplayAlertAsync("Confirmar chamado",
            "Enviar esta solicitação à ouvidoria do clube selecionado?", "Enviar", "Voltar")) return;
        _ocupado = true;
        Enviar.IsEnabled = false;
        Indicador.IsVisible = Indicador.IsRunning = true;
        try
        {
            var (ok, mensagem) = await _api.CriarChamadoAsync(
                _clubes[Clube.SelectedIndex].Id, tipo, categoria, assunto, descricao);
            if (!ok) { Mensagem.Text = mensagem; return; }
            await DisplayAlertAsync("Chamado enviado",
                "A API registrou o chamado. Consulte o histórico para acompanhar.", "OK");
            await Navigation.PopAsync();
        }
        finally
        {
            _ocupado = false;
            Enviar.IsEnabled = true;
            Indicador.IsVisible = Indicador.IsRunning = false;
        }
    }
}
