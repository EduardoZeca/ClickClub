namespace ClickClub.Mobile.Modelos;

public sealed record AvisoMobile(long Id, string Titulo, string Corpo,
    string Clube, string Inicio, string Fim);

public sealed record ClubeOuvidoriaMobile(long Id, string Nome);

public sealed record ChamadoOuvidoriaMobile(long Id, string Tipo, string Categoria,
    string Assunto, string Descricao, string Clube, string Status, string CriadoEm);

public sealed record RespostaOuvidoriaMobile(string Autor, string Conteudo, string DataHora);

public sealed record ChamadoOuvidoriaDetalheMobile(ChamadoOuvidoriaMobile Chamado,
    IReadOnlyList<RespostaOuvidoriaMobile> Respostas);

public sealed record FichaMobile(string Matricula, string Situacao,
    string Titular, string Categoria, string TipoVinculo, string Clube,
    string DataInicio, IReadOnlyList<string> Dependentes);
