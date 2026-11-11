using ClickClub.Mobile.Servicos;

namespace ClickClub.Mobile.Paginas;

public partial class InicioPage : ContentPage
{
    private readonly SessaoLocal _sessao;
    private readonly ApiClickClub _api;
    private readonly CarteirinhaLocal _cache;

    public InicioPage(SessaoLocal sessao, ApiClickClub api, CarteirinhaLocal cache)
    {
        InitializeComponent();
        _sessao = sessao;
        _api = api;
        _cache = cache;
    }

    private async void AoAbrirCarteirinha(object sender, EventArgs e) =>
        await Navigation.PushAsync(new CarteirinhaPage(_api, _cache));

    private async void AoAbrirClubes(object sender, EventArgs e) =>
        await Navigation.PushAsync(new ClubesPage(_api));

    private async void AoAbrirReservas(object sender, EventArgs e) =>
        await Navigation.PushAsync(new MinhasReservasPage(_api));

    private async void AoAbrirFinanceiro(object sender, EventArgs e) =>
        await Navigation.PushAsync(new FinanceiroPage(_api));

    private async void AoAbrirAtividades(object sender, EventArgs e) =>
        await Navigation.PushAsync(new AtividadesEventosPage(_api));

    private async void AoAbrirMural(object sender, EventArgs e) =>
        await Navigation.PushAsync(new MuralPage(_api));

    private async void AoAbrirOuvidoria(object sender, EventArgs e) =>
        await Navigation.PushAsync(new OuvidoriaPage(_api));

    private async void AoAbrirFicha(object sender, EventArgs e) =>
        await Navigation.PushAsync(new FichaAssociativaPage(_api));

    private async void AoAbrirAdesao(object sender, EventArgs e) =>
        await Navigation.PushAsync(new AdesaoPage());

    private async void AoSair(object sender, EventArgs e)
    {
        var confirmar = await DisplayAlertAsync("Sair", "Encerrar a sessão e apagar a carteirinha salva neste aparelho?", "Sair", "Cancelar");
        if (!confirmar) return;
        _sessao.LimparToken();
        _cache.Limpar();
        ((App)Application.Current!).AbrirLogin();
    }
}
