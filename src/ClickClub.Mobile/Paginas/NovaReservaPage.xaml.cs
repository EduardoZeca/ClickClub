using ClickClub.Mobile.Modelos;
using ClickClub.Mobile.Servicos;

namespace ClickClub.Mobile.Paginas;

public partial class NovaReservaPage : ContentPage
{
    private readonly ApiClickClub _api;
    private readonly ClubeMobile _clube;
    private readonly EspacoMobile _espaco;
    private bool _enviando;

    public NovaReservaPage(ApiClickClub api, ClubeMobile clube, EspacoMobile espaco)
    {
        InitializeComponent();
        _api = api;
        _clube = clube;
        _espaco = espaco;
        DetalhesEspaco.Text = $"{espaco.Nome} · {clube.Nome}";
        var dia = DateTime.Today.AddDays(2);
        DiaInicio.Date = dia;
        DiaFim.Date = dia;
        HoraInicio.Time = new TimeSpan(14, 0, 0);
        HoraFim.Time = new TimeSpan(16, 0, 0);
    }

    private async void AoConfirmar(object sender, EventArgs e)
    {
        if (_enviando) return;
        if (DiaInicio.Date is not DateTime dataInicio ||
            DiaFim.Date is not DateTime dataFim ||
            HoraInicio.Time is not TimeSpan horaInicio ||
            HoraFim.Time is not TimeSpan horaFim)
        {
            Mensagem.Text = "Informe datas e horários completos.";
            return;
        }

        var inicio = dataInicio.Date + horaInicio;
        var fim = dataFim.Date + horaFim;
        if (inicio >= fim)
        {
            Mensagem.Text = "O término deve ser posterior ao início. A API validará o horário do clube.";
            return;
        }

        var convidados = new List<ConvidadoMobile>();
        var linhas = (ConvidadosEntrada.Text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var linha in linhas)
        {
            var partes = linha.Trim().Split('|', 2, StringSplitOptions.TrimEntries);
            if (partes.Length != 2 || partes[0].Length < 2 || partes[1].Length < 3)
            {
                Mensagem.Text = "Cada convidado deve seguir: Nome | Identificação.";
                return;
            }
            convidados.Add(new ConvidadoMobile(partes[0], partes[1]));
        }

        var confirmado = await DisplayAlertAsync("Confirmar reserva",
            $"Solicitar { _espaco.Nome } de {inicio:dd/MM/yyyy HH:mm} até {fim:dd/MM/yyyy HH:mm}? A API determinará o valor e a elegibilidade.",
            "Confirmar", "Voltar");
        if (!confirmado) return;

        _enviando = true;
        BotaoConfirmar.IsEnabled = false;
        Indicador.IsVisible = Indicador.IsRunning = true;
        try
        {
            var (ok, mensagem) = await _api.CriarReservaAsync(new NovaReservaMobile(
                _espaco.Id, inicio, fim, FinalidadeEntrada.Text?.Trim() ?? "", convidados));
            Mensagem.Text = mensagem;
            if (ok)
            {
                await DisplayAlertAsync("Reserva enviada",
                    "A API confirmou a operação. Consulte Minhas reservas para acompanhar.", "OK");
                await Navigation.PopAsync();
            }
        }
        finally
        {
            Indicador.IsVisible = Indicador.IsRunning = false;
            BotaoConfirmar.IsEnabled = true;
            _enviando = false;
        }
    }
}
