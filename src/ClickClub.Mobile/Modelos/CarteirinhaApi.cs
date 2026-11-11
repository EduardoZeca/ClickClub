namespace ClickClub.Mobile.Modelos;

public sealed class CarteirinhaApi
{
    public string CodigoQr { get; set; } = "";
    public string IdentificadorPublico { get; set; } = "";
    public DateTimeOffset EmitidaEmUtc { get; set; }
    public DateTimeOffset ValidaAteUtc { get; set; }
    public int Versao { get; set; }
    public bool EmitidaAgora { get; set; }

    public bool EstaValida =>
        !string.IsNullOrWhiteSpace(CodigoQr) && ValidaAteUtc > DateTimeOffset.UtcNow;
}
