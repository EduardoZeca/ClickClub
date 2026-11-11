using ClickClub.Mobile.Servicos;

namespace ClickClub.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<SessaoLocal>();
        builder.Services.AddSingleton<CarteirinhaLocal>();
        builder.Services.AddSingleton<ApiClickClub>();
        return builder.Build();
    }
}
