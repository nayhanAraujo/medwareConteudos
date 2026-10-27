using System.Data.Common;
using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Orcamentos;

public static class ClientAdminSchema
{
    public static readonly (string Table,string Definition)[] Definitions = [
        ("CLIENTEADMIN","CODCLIENTE INTEGER NOT NULL, CNPJ CHAR(14) CHARACTER SET ASCII NOT NULL, DOCUMENTO BLOB SUB_TYPE TEXT CHARACTER SET UTF8 NOT NULL, CODUSUARIO INTEGER NOT NULL, DTHRCRIACAO TIMESTAMP NOT NULL, CONSTRAINT PK_CLIENTEADMIN PRIMARY KEY(CODCLIENTE), CONSTRAINT UNQ_CLIENTEADMIN_CNPJ UNIQUE(CNPJ), CONSTRAINT FK_CLIENTEADMIN_CODCLIENTE FOREIGN KEY(CODCLIENTE) REFERENCES CLIENTES(CODCLIENTE)"),
        ("CLIENTEADMINCONSULTA","CODCLIENTEADMINCONSULTA CHAR(36) CHARACTER SET ASCII NOT NULL, CODUSUARIO INTEGER NOT NULL, DOCUMENTO BLOB SUB_TYPE TEXT CHARACTER SET UTF8 NOT NULL, DTHRCRIACAO TIMESTAMP NOT NULL, DTHREXPIRACAO TIMESTAMP NOT NULL, CONSUMIDA SMALLINT DEFAULT 0 NOT NULL, CONSTRAINT PK_CLIENTEADMINCONSULTA PRIMARY KEY(CODCLIENTEADMINCONSULTA), CONSTRAINT CK_CLIADMCONS_CONSUMIDA CHECK(CONSUMIDA IN (0,1))")
    ];
    public static async Task Migrate(IFirebirdConnectionFactory db,CancellationToken ct)
    {
        await using var c=await db.OpenConnectionAsync(ct);
        foreach(var (table,definition) in Definitions)
            if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME=@table",new{table},cancellationToken:ct))==0)
                await c.ExecuteAsync(new CommandDefinition($"CREATE TABLE {table}({definition})",cancellationToken:ct));
        // Firebird exposes newly added columns to DML only after the DDL commits.
        await using(var add=await c.BeginTransactionAsync(ct))
        {
            if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='CLIENTEADMIN' AND RDB$FIELD_NAME='DOCUMENTOFISCAL'",transaction:add,cancellationToken:ct))==0)
                await c.ExecuteAsync(new CommandDefinition("ALTER TABLE CLIENTEADMIN ADD DOCUMENTOFISCAL VARCHAR(14) CHARACTER SET ASCII",transaction:add,cancellationToken:ct));
            await add.CommitAsync(ct);
        }
        await using(var tx=await c.BeginTransactionAsync(ct))
        {
            await c.ExecuteAsync(new CommandDefinition("UPDATE CLIENTEADMIN SET DOCUMENTOFISCAL=TRIM(CNPJ) WHERE DOCUMENTOFISCAL IS NULL",transaction:tx,cancellationToken:ct));
            await c.ExecuteAsync(new CommandDefinition("ALTER TABLE CLIENTEADMIN ALTER DOCUMENTOFISCAL SET NOT NULL",transaction:tx,cancellationToken:ct));
            if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='CLIENTEADMIN' AND RDB$FIELD_NAME='CNPJ' AND RDB$NULL_FLAG=1",transaction:tx,cancellationToken:ct))>0)
                await c.ExecuteAsync(new CommandDefinition("ALTER TABLE CLIENTEADMIN ALTER CNPJ DROP NOT NULL",transaction:tx,cancellationToken:ct));
            if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_CONSTRAINTS WHERE RDB$CONSTRAINT_NAME='UNQ_CLIENTEADMIN_DOCFISCAL'",transaction:tx,cancellationToken:ct))==0)
                await c.ExecuteAsync(new CommandDefinition("ALTER TABLE CLIENTEADMIN ADD CONSTRAINT UNQ_CLIENTEADMIN_DOCFISCAL UNIQUE(DOCUMENTOFISCAL)",transaction:tx,cancellationToken:ct));
            await tx.CommitAsync(ct);
        }
        await Validate(c,ct);
    }
    public static async Task Validate(DbConnection c,CancellationToken ct)
    {
        if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_CONSTRAINTS WHERE RDB$CONSTRAINT_NAME IN ('PK_CLIENTEADMIN','UNQ_CLIENTEADMIN_CNPJ','FK_CLIENTEADMIN_CODCLIENTE','PK_CLIENTEADMINCONSULTA','CK_CLIADMCONS_CONSUMIDA')",cancellationToken:ct))!=5
            ||await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='CLIENTEADMIN' AND RDB$FIELD_NAME IN ('CODCLIENTE','CNPJ','DOCUMENTO','CODUSUARIO','DTHRCRIACAO')",cancellationToken:ct))!=5
            ||await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='CLIENTEADMINCONSULTA' AND RDB$FIELD_NAME IN ('CODCLIENTEADMINCONSULTA','CODUSUARIO','DOCUMENTO','DTHRCRIACAO','DTHREXPIRACAO','CONSUMIDA')",cancellationToken:ct))!=6)
            throw new InvalidOperationException("Vínculos Admin de clientes ausentes. Execute --migrate-database antes de iniciar a API.");
        if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='CLIENTEADMIN' AND RDB$FIELD_NAME='DOCUMENTOFISCAL' AND RDB$NULL_FLAG=1",cancellationToken:ct))!=1
            ||await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_CONSTRAINTS WHERE RDB$CONSTRAINT_NAME='UNQ_CLIENTEADMIN_DOCFISCAL'",cancellationToken:ct))!=1)
            throw new InvalidOperationException("Vínculo CPF/CNPJ pendente. Execute --migrate-database antes de iniciar a API.");
    }
}
