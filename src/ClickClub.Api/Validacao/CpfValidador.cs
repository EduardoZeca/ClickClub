namespace ClickClub.Api.Validacao;

public static class CpfValidador
{
    public static bool Valido(string? cpf)
    {
        if (cpf is null || cpf.Length != 11 ||
            cpf.Any(c => c is < '0' or > '9') ||
            cpf.All(c => c == cpf[0]))
            return false;

        for (var indice = 9; indice <= 10; indice++)
        {
            var soma = 0;
            for (var i = 0; i < indice; i++)
                soma += (cpf[i] - '0') * (indice + 1 - i);

            var resto = (soma * 10) % 11;
            if (resto == 10) resto = 0;
            if (resto != cpf[indice] - '0')
                return false;
        }

        return true;
    }
}
