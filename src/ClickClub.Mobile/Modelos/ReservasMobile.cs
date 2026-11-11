namespace ClickClub.Mobile.Modelos;

public sealed record ClubeMobile(long Id, string Nome, string Municipio, string Uf,
    string FusoHorario);

public sealed record EspacoMobile(long Id, long UnidadeId, string Nome, string Tipo,
    int Capacidade, decimal TaxaBase, bool Ativo);

public sealed record ReservaMobile(long Id, string Espaco, string Clube,
    string Inicio, string Fim, string Situacao, decimal Valor,
    string Finalidade);

public sealed record ConvidadoMobile(string Nome, string IdentificacaoMinima);

public sealed record NovaReservaMobile(long EspacoId, DateTime InicioLocal,
    DateTime FimLocal, string Finalidade, IReadOnlyList<ConvidadoMobile> Convidados);
