using ClickClub.Api.Dados;
using Microsoft.AspNetCore.Identity;

namespace ClickClub.Api.Seguranca;

public static class AdministradorInicial
{
    public static async Task ExecutarAsync(IServiceProvider services)
    {
        var repositorio = services.GetRequiredService<AcessoRepositorio>();
        var hasher = services.GetRequiredService<IPasswordHasher<ContaAcesso>>();

        if (await repositorio.ExisteAdministradorGeralAsync())
        {
            Console.WriteLine(
                "Já existe um Administrador Geral. Inicialização cancelada.");
            return;
        }

        if (Console.IsInputRedirected)
        {
            Console.WriteLine(
                "Execute este comando em um terminal interativo do Windows.");
            return;
        }

        Console.Write("Nome do primeiro administrador: ");
        var nome = Console.ReadLine()?.Trim() ?? string.Empty;

        Console.Write("Login do primeiro administrador: ");
        var login = Console.ReadLine()?.Trim() ?? string.Empty;

        if (nome.Length is < 2 or > 150 || login.Length is < 3 or > 100)
        {
            Console.WriteLine("Nome ou login fora dos limites permitidos.");
            return;
        }

        Console.Write("Senha (mínimo de 12 caracteres): ");
        var senha = LerSenha();
        Console.Write("Repita a senha: ");
        var confirmacao = LerSenha();

        if (senha.Length < 12 || senha != confirmacao)
        {
            Console.WriteLine("Senha inválida ou confirmação diferente.");
            return;
        }

        var conta = new ContaAcesso { Nome = nome, Login = login };
        var senhaHash = hasher.HashPassword(conta, senha);

        await repositorio.CriarAdministradorInicialAsync(nome, login, senhaHash);
        Console.WriteLine("Administrador Geral criado com sucesso.");
    }

    private static string LerSenha()
    {
        var caracteres = new List<char>();

        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);

            if (tecla.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (caracteres.Count > 0)
                {
                    caracteres.RemoveAt(caracteres.Count - 1);
                    Console.Write("\b \b");
                }
                continue;
            }

            if (!char.IsControl(tecla.KeyChar) && caracteres.Count < 200)
            {
                caracteres.Add(tecla.KeyChar);
                Console.Write('*');
            }
        }

        return new string(caracteres.ToArray());
    }
}
