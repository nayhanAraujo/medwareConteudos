using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.PaineisCadastros;

public sealed class PaineisCadastrosService(IFirebirdConnectionFactory connections) : IPaineisCadastrosService
{
    public async Task<IReadOnlyList<PainelCadastroItem>> ListAsync(PainelCadastroTipo tipo, CancellationToken ct)
    {
        var definition = Definition(tipo);
        await using var connection = await connections.OpenConnectionAsync(ct);
            var rows = await connection.QueryAsync<PainelCadastroRow>(new CommandDefinition($"""
            SELECT c.{definition.IdColumn} AS Id,
                   c.NOME AS Nome,
                   CAST((SELECT COUNT(*) FROM {definition.LinkTable} p WHERE p.{definition.LinkColumn} = c.{definition.IdColumn}) AS INTEGER) AS QuantidadeVinculos
              FROM {definition.Table} c
             ORDER BY c.NOME
            """, cancellationToken: ct));

        return rows.Select(row => new PainelCadastroItem(
            row.Id,
            row.Nome,
            row.QuantidadeVinculos,
            tipo == PainelCadastroTipo.Cliente && row.Id == 1)).ToList();
    }

    public async Task CreateAsync(PainelCadastroTipo tipo, PainelCadastroRequest request, CancellationToken ct)
    {
        var nome = NormalizeName(request.Nome);
        var definition = Definition(tipo);
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await EnsureUniqueName(connection, transaction, definition, nome, null, ct);
            var sql = tipo == PainelCadastroTipo.Cliente
                ? $"INSERT INTO {definition.Table} (NOME, STATUS) VALUES (@Nome, 1)"
                : $"INSERT INTO {definition.Table} (NOME) VALUES (@Nome)";
            await connection.ExecuteAsync(new CommandDefinition(sql, new { Nome = nome }, transaction, cancellationToken: ct));
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(PainelCadastroTipo tipo, int id, PainelCadastroRequest request, CancellationToken ct)
    {
        var nome = NormalizeName(request.Nome);
        var definition = Definition(tipo);
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await EnsureUniqueName(connection, transaction, definition, nome, id, ct);
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                $"UPDATE {definition.Table} SET NOME = @Nome WHERE {definition.IdColumn} = @Id",
                new { Nome = nome, Id = id }, transaction, cancellationToken: ct));
            await transaction.CommitAsync(ct);
            return affected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(PainelCadastroTipo tipo, int id, CancellationToken ct)
    {
        if (tipo == PainelCadastroTipo.Cliente && id == 1)
            throw new InvalidOperationException("O cliente padrão não pode ser excluído.");

        var definition = Definition(tipo);
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            var links = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                $"SELECT COUNT(*) FROM {definition.LinkTable} WHERE {definition.LinkColumn} = @Id",
                new { Id = id }, transaction, cancellationToken: ct));
            if (links > 0)
                throw new InvalidOperationException($"O registro possui {links} painel(is) vinculado(s) e não pode ser excluído.");

            var affected = await connection.ExecuteAsync(new CommandDefinition(
                $"DELETE FROM {definition.Table} WHERE {definition.IdColumn} = @Id",
                new { Id = id }, transaction, cancellationToken: ct));
            await transaction.CommitAsync(ct);
            return affected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task EnsureUniqueName(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CadastroDefinition definition,
        string nome,
        int? id,
        CancellationToken ct)
    {
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(*)
              FROM {definition.Table}
             WHERE UPPER(TRIM(NOME)) = UPPER(TRIM(@Nome))
               AND (@Id IS NULL OR {definition.IdColumn} <> @Id)
            """, new { Nome = nome, Id = id }, transaction, cancellationToken: ct));
        if (count > 0)
            throw new InvalidOperationException("Já existe um registro com este nome.");
    }

    private static string NormalizeName(string? name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("O nome é obrigatório.");
        if (normalized.Length > 100)
            throw new InvalidOperationException("O nome deve ter no máximo 100 caracteres.");
        return normalized;
    }

    private static CadastroDefinition Definition(PainelCadastroTipo tipo) => tipo switch
    {
        PainelCadastroTipo.Cliente => new("Clientes", "CODCLIENTE", "Paineis", "CODCLIENTE"),
        PainelCadastroTipo.Modulo => new("ModulosSistema", "CODMODULO", "Paineis", "CODMODULO"),
        PainelCadastroTipo.PacoteComercial => new("PacotesComerciais", "CODPACOTECOMERCIAL", "Paineis_Pacotes", "CODPACOTECOMERCIAL"),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo))
    };

    private sealed record CadastroDefinition(string Table, string IdColumn, string LinkTable, string LinkColumn);
    private sealed record PainelCadastroRow(int Id, string Nome, int QuantidadeVinculos);
}
