using ClickClub.Mobile.Servicos;

namespace ClickClub.Mobile.Paginas;

public partial class LoginPage : ContentPage
{
    private readonly ApiClickClub _api;
    private readonly SessaoLocal _sessao;
    private readonly CarteirinhaLocal _cache;
    private bool _consultouSessao;

    public LoginPage(ApiClickClub api, SessaoLocal sessao, CarteirinhaLocal cache)
    {
        InitializeComponent();
        _api = api;
        _sessao = sessao;
        _cache = cache;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_consultouSessao) return;
        _consultouSessao = true;
        var token = await _sessao.LerTokenAsync();
        // Mesmo sem rede, o usuário autenticado anteriormente poderá consultar seu QR salvo.
        if (!string.IsNullOrWhiteSpace(token))
            ((App)Application.Current!).AbrirInicio();
    }

    private async void AoAbrirAdesao(object sender, EventArgs e) =>
        await Navigation.PushAsync(new AdesaoPage());

    private async void AoEntrar(object sender, EventArgs e)
    {
        var login = EntradaUsuario.Text?.Trim() ?? "";
        var senha = EntradaSenha.Text ?? "";
        if (login.Length == 0 || senha.Length == 0)
        {
            Mensagem.Text = "Informe usuário e senha.";
            return;
        }
        BotaoEntrar.IsEnabled = false;
        Indicador.IsVisible = Indicador.IsRunning = true;
        Mensagem.Text = "Autenticando...";
        try
        {
            var (ok, mensagem) = await _api.EntrarAsync(login, senha);
            if (ok)
            {
                // Não manter QR de um usuário anterior depois de autenticar outra pessoa.
                _cache.Limpar();
                EntradaSenha.Text = "";
                ((App)Application.Current!).AbrirInicio();
                return;
            }
            Mensagem.Text = mensagem;
        }
        finally
        {
            Indicador.IsVisible = Indicador.IsRunning = false;
            BotaoEntrar.IsEnabled = true;
        }
    }
}
