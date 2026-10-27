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
        await Validate(c,ct);
    }
    public static async Task Validate(DbConnection c,CancellationToken ct)
    {
        if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_CONSTRAINTS WHERE RDB$CONSTRAINT_NAME IN ('PK_CLIENTEADMIN','UNQ_CLIENTEADMIN_CNPJ','FK_CLIENTEADMIN_CODCLIENTE','PK_CLIENTEADMINCONSULTA','CK_CLIADMCONS_CONSUMIDA')",cancellationToken:ct))!=5
            ||await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='CLIENTEADMIN' AND RDB$FIELD_NAME IN ('CODCLIENTE','CNPJ','DOCUMENTO','CODUSUARIO','DTHRCRIACAO')",cancellationToken:ct))!=5
            ||await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='CLIENTEADMINCONSULTA' AND RDB$FIELD_NAME IN ('CODCLIENTEADMINCONSULTA','CODUSUARIO','DOCUMENTO','DTHRCRIACAO','DTHREXPIRACAO','CONSUMIDA')",cancellationToken:ct))!=6)
            throw new InvalidOperationException("Vínculos Admin de clientes ausentes. Execute --migrate-database antes de iniciar a API.");
    }
}
