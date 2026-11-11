using Microsoft.Extensions.DependencyInjection;
using ClickClub.Mobile.Paginas;
using ClickClub.Mobile.Servicos;

namespace ClickClub.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _servicos;
    private Window? _janela;

    public App(IServiceProvider servicos)
    {
        InitializeComponent();
        _servicos = servicos;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _janela = new Window(new NavigationPage(NovaPaginaLogin()));
        return _janela;
    }

    private LoginPage NovaPaginaLogin() => new(
        _servicos.GetRequiredService<ApiClickClub>(),
        _servicos.GetRequiredService<SessaoLocal>(),
        _servicos.GetRequiredService<CarteirinhaLocal>());

    public void AbrirInicio()
    {
        if (_janela is not null)
            _janela.Page = new NavigationPage(new InicioPage(
                _servicos.GetRequiredService<SessaoLocal>(),
                _servicos.GetRequiredService<ApiClickClub>(),
                _servicos.GetRequiredService<CarteirinhaLocal>()));
    }

    public void AbrirLogin()
    {
        if (_janela is not null)
            _janela.Page = new NavigationPage(NovaPaginaLogin());
    }
}
