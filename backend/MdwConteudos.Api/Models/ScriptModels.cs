namespace MdwConteudos.Api.Models;

public record PacoteDto(int CodPacote, string Nome, string? Descricao);

public record ScriptVariableDto(string Variavel, string Nome);

public record ScriptFileDto(string Tipo, string Caminho, string NomeArquivo);

public record ScriptMrdDto(int CodScriptMrd, string NomeArquivo, bool Padrao, int? Ordem);

public record ScriptListItemDto(
    int CodScriptLaudo,
    string Nome,
    string? Descricao,
    string? Linguagem,
    string? CaminhoProjeto,
    string Sistema,
    int Aprovado,
    DateTime? DataVerificacao,
    int Ativo,
    bool TemArquivoJson,
    string? AprovadoPor,
    string? NomePacote,
    string? CaminhoAzure,
    bool TemArquivoDll,
    bool TemArquivoMrd,
    string? CriadoPor,
    string? LinkTeste,
    string? UltimaVersao,
    int? CodUltimaVersao,
    IReadOnlyList<ScriptVariableDto> Variaveis,
    IReadOnlyList<ScriptFileDto> ImagensDisplay,
    IReadOnlyList<ScriptFileDto> PdfsDisplay,
    IReadOnlyList<ScriptMrdDto> MrdList
);

public record PagedScriptsResponse(
    IReadOnlyList<ScriptListItemDto> Items,
    int Page,
    int TotalPages,
    int TotalItems
);

public record ScriptDetailDto(
    int CodScriptLaudo,
    string Nome,
    string? Descricao,
    string? Linguagem,
    string? CaminhoProjeto,
    string? CaminhoAzure,
    string? LinkTeste,
    string Sistema,
    int? CodPacote,
    int Aprovado,
    string? AprovadoPor,
    int Ativo,
    string? CriadoPor,
    bool TemArquivoJson,
    bool TemArquivoDll,
    IReadOnlyList<ScriptMrdDto> MrdList,
    IReadOnlyList<ScriptFileDto> Imagens,
    IReadOnlyList<ScriptFileDto> Pdfs,
    IReadOnlyList<int> VariaveisAssociadas
);

public record VersaoDto(
    int CodVersao,
    string NumeroVersao,
    DateTime? DataCriacao,
    string? Ativo,
    string? Aprovado,
    string? Observacoes,
    string? UsuarioResponsavel,
    string? DescricaoAlteracoes,
    string? AprovadoPor
);

public record VersaoCreateMetaDto(
    string NomeScript,
    string? DescricaoScript,
    string Sistema,
    string? Linguagem,
    string? NomePacote,
    string ProximaVersao,
    IReadOnlyList<VersaoDto> VersoesExistentes
);

public record VersaoExportMetaDto(string NomeScript, string NumeroVersao, string Sistema, string? Linguagem);

public record ScriptVersionFileDto(
    int CodArquivo,
    string Tipo,
    string? Caminho,
    string NomeArquivo,
    DateTime? DataUpload,
    string? UsuarioUpload
);

public record ScriptVersionMrdDto(
    int CodVersaoMrd,
    string NomeArquivo,
    bool Padrao,
    int? Ordem
);

public record ScriptVersionHistoryDto(
    string TipoAlteracao,
    string? Descricao,
    string? Usuario,
    DateTime? DataAlteracao
);

public record ScriptVersionDetailDto(
    int CodVersao,
    int CodScriptLaudo,
    string NumeroVersao,
    string NomeScript,
    string? DescricaoScript,
    string Sistema,
    string? Linguagem,
    string? NomePacote,
    string? DescricaoAlteracoes,
    string? AlteracoesInterface,
    string? AlteracoesCodigo,
    DateTime? DataCriacao,
    string? UsuarioResponsavel,
    string? Ativo,
    string? Aprovado,
    string? AprovadoPor,
    DateTime? DataAprovacao,
    string? Observacoes,
    bool TemArquivoJson,
    bool TemArquivoDll,
    IReadOnlyList<ScriptVersionFileDto> Imagens,
    IReadOnlyList<ScriptVersionFileDto> Pdfs,
    IReadOnlyList<ScriptVersionMrdDto> MrdList,
    IReadOnlyList<ScriptVersionHistoryDto> Historico
);

public record ApprovalTokenInfo(
    string NomeScript,
    string Sistema,
    string? Linguagem,
    string? LinkTeste,
    DateTime? ExpiraEm,
    bool JaAprovado,
    bool TokenUsado
);
