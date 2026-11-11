using System.Net;
using ClickClub.Mobile.Modelos;
using ClickClub.Mobile.Servicos;
using QRCoder;

namespace ClickClub.Mobile.Paginas;

public partial class CarteirinhaPage : ContentPage
{
    private readonly ApiClickClub _api;
    private readonly CarteirinhaLocal _cache;
    private bool _abriu;
    private bool _emOperacao;
    private bool _visivel;
    private CarteirinhaApi? _credencialAtual;

    public CarteirinhaPage(ApiClickClub api, CarteirinhaLocal cache)
    {
        InitializeComponent();
        _api = api;
        _cache = cache;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _visivel = true;
        Dispatcher.StartTimer(TimeSpan.FromSeconds(30), () =>
        {
            if (!_visivel) return false;
            if (_credencialAtual is { EstaValida: false })
                MostrarCredencial(null, "A credencial expirou. Reconecte para renovar.");
            if (!_emOperacao && (_credencialAtual is null ||
                _credencialAtual.ValidaAteUtc <= DateTimeOffset.UtcNow.AddHours(1)))
                _ = AtualizarAsync();
            return true;
        });
        if (_abriu) return;
        _abriu = true;
        await MostrarCacheAsync();
        await AtualizarAsync();
    }

    protected override void OnDisappearing()
    {
        _visivel = false;
        base.OnDisappearing();
    }

    private async void AoAtualizar(object sender, EventArgs e) => await AtualizarAsync();

    private async Task MostrarCacheAsync()
    {
        var credencial = await _cache.LerAsync();
        MostrarCredencial(credencial, credencial?.EstaValida == true
            ? "Credencial armazenada neste aparelho. A portaria validará online."
            : "Não há uma credencial válida salva neste aparelho.");
    }

    private async Task AtualizarAsync()
    {
        if (_emOperacao) return;
        _emOperacao = true;
        BotaoAtualizar.IsEnabled = false;
        Indicador.IsVisible = Indicador.IsRunning = true;
        try
        {
            var (credencial, status, mensagem) = await _api.AtualizarCarteirinhaAsync();
            if (credencial is not null)
            {
                await _cache.GuardarAsync(credencial);
                MostrarCredencial(credencial, "Credencial atualizada pela API. A validação da entrada é online.");
            }
            else if (status is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Conflict)
            {
                // Bloqueio confirmado pela API: não apresentar cache como válido.
                _cache.Limpar();
                MostrarCredencial(null, mensagem);
            }
            else
            {
                // A API pode estar desligada ou a sessão expirada; exibição offline tem validade limitada.
                await MostrarCacheAsync();
                Mensagem.Text = mensagem + " Não há liberação offline na portaria.";
            }
        }
        catch (Exception)
        {
            Mensagem.Text = "Não foi possível proteger a credencial no dispositivo. Verifique o armazenamento local.";
            MostrarCredencial(null, Mensagem.Text);
        }
        finally
        {
            Indicador.IsVisible = Indicador.IsRunning = false;
            BotaoAtualizar.IsEnabled = true;
            _emOperacao = false;
        }
    }

    private void MostrarCredencial(CarteirinhaApi? credencial, string mensagem)
    {
        _credencialAtual = credencial;
        var vigente = credencial?.EstaValida == true;
        PainelQr.IsVisible = vigente;
        Situacao.Text = vigente ? "QR Code dentro da validade" : "Carteirinha indisponível ou expirada";
        Mensagem.Text = mensagem;
        if (!vigente || credencial is null)
        {
            ImagemQr.Source = null;
            return;
        }
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(credencial.CodigoQr, QRCodeGenerator.ECCLevel.M);
        using var imagem = new PngByteQRCode(dados);
        var bytes = imagem.GetGraphic(6);
        ImagemQr.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
        Identificador.Text = $"Identificação: {credencial.IdentificadorPublico}";
        Validade.Text = $"Válida até {credencial.ValidaAteUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
    }
}
