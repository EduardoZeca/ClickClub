namespace ClickClub.Mobile.Servicos;

public static class ApiConfiguracao
{
    // Android físico: usar adb reverse tcp:5201 tcp:5201 no computador.
    // Emulador: substituir por http://10.0.2.2:5201/.
    // Não colocar a chave JWT, segredos ou senhas neste arquivo.
    public const string UrlBase = "http://127.0.0.1:5201/";
}
