using System.Data.Common;
using Dapper;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed record ResolveAdminClientRequest(int? CodigoAdmin=null,string? Cnpj=null,string? Name=null);
public sealed record AdminClientResolution(string State,string Message,AdminClientSnapshot? Candidate=null,int? LocalId=null,bool CanImport=false,List<AdminLocalCandidate>? Suggestions=null);
public sealed record AdminLocalCandidate(int LocalId,string Name,string? Cnpj,bool Verified);
public sealed record AdminClientPreviewRequest(int CodigoAdmin,string? Cnpj=null);
public sealed record ImportAdminClientRequest(Guid ConsultationId,bool Confirm,string Reason);
public sealed record AdminClientPreview(AdminClientResolution Resolution,Guid? ConsultationId,string? ExpiresAtUtc);
public sealed record AdminClientImportResult(int Id,int CodigoAdmin,bool Created);
public sealed record AdminClientProof(AdminClientSnapshot Candidate,string Fingerprint,string? ImportReason=null);

public sealed partial class OrcamentoService
{
    public async Task<AdminClientResolution> ResolveAdminClient(ResolveAdminClientRequest request,IAdminClientDirectory directory,CancellationToken ct)
    {
        var cnpj=string.IsNullOrWhiteSpace(request.Cnpj)?null:AdminClientIdentity.Cnpj(request.Cnpj);
        await using var c=await db.OpenConnectionAsync(ct);_=await Actor(c,null,ct);
        if(request.CodigoAdmin is { } code)
        {
            if(code<=0)throw new OrcamentoException(422,"Informe um código Admin positivo.");
            var official=await directory.GetAsync(code,ct);
            if(official is null)return new("nao_encontrado","Código não encontrado na API Admin. Não cadastrar por nome inferido.");
            official=AdminClientIdentity.Validate(official);
            if(official.CodigoAdmin!=code)throw new OrcamentoException(502,"Código retornado pelo Admin é diferente do solicitado.");
            if(cnpj is not null&&cnpj!=official.Cnpj)return new("conflito","O CNPJ informado não corresponde ao código Admin.",official);
            return await CheckAdminIdentity(c,null,official,ct);
        }
        if(cnpj is not null)
        {
            var locals=await AdminLocalCandidates(c,null,ct);
            var matches=locals.Where(r=>r.Cnpj==cnpj).ToList();
            if(matches.Count==1&&matches[0].Verified)return new("encontrado_local","Vínculo local Admin verificado. A origem e o destinatário do orçamento ainda exigem confirmação humana.",LocalId:matches[0].LocalId,Suggestions:matches);
            return new(matches.Count>0?"revisao_necessaria":"nao_encontrado",matches.Count>0?"Há cadastro local com este CNPJ, mas a identidade Admin precisa ser conferida por código.":"CNPJ não encontrado no catálogo local. Informe codigoAdmin para consultar a API Admin; busca remota por CNPJ ainda não tem contrato confirmado.",Suggestions:matches.Take(10).ToList());
        }
        QuoteCalculator.CheckText(request.Name,"Nome para sugestão",100);
        if(string.IsNullOrWhiteSpace(request.Name)||request.Name.Trim().Length<3)throw new OrcamentoException(422,"Informe código Admin, CNPJ ou pelo menos três caracteres do nome.");
        var suggestions=(await c.QueryAsync<AdminLocalCandidateRow>(new CommandDefinition("SELECT FIRST 11 CODCLIENTE AS Id,NOME AS Name,STATUS AS Status FROM CLIENTES WHERE NOME CONTAINING @name ORDER BY CODCLIENTE",new{name=request.Name.Trim()},cancellationToken:ct))).Select(r=>new AdminLocalCandidate(r.Id,r.Name,null,false)).ToList();
        return new(suggestions.Count>0?"revisao_necessaria":"nao_encontrado","Nome produz apenas sugestões, nunca confirmação de identidade nem cadastro automático. Informe código Admin/CNPJ confiável.",Suggestions:suggestions.Take(10).ToList());
    }
    public async Task<AdminClientPreview> PreviewAdminClient(AdminClientPreviewRequest request,IAdminClientDirectory directory,CancellationToken ct)
    {
        var resolution=await ResolveAdminClient(new(request.CodigoAdmin,request.Cnpj),directory,ct);
        if(!resolution.CanImport||resolution.Candidate is null)return new(resolution,null,null);
        await using var c=await db.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);var actor=await Actor(c,tx,ct);
        var consultation=Guid.NewGuid();var expires=Now.AddMinutes(15);var candidate=resolution.Candidate;
        await c.ExecuteAsync(new CommandDefinition("INSERT INTO CLIENTEADMINCONSULTA(CODCLIENTEADMINCONSULTA,CODUSUARIO,DOCUMENTO,DTHRCRIACAO,DTHREXPIRACAO,CONSUMIDA) VALUES(@id,@actor,@json,@now,@expires,0)",new{id=consultation.ToString("D"),actor=actor.Id,json=QuoteTextBlob.Json(new AdminClientProof(candidate,QuoteEmailSourceValidation.Hash(candidate))),now=Now,expires},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);return new(resolution,consultation,QuoteCalculator.Utc(expires));
    }
    public async Task<AdminClientImportResult> ImportAdminClient(ImportAdminClientRequest request,IAdminClientDirectory directory,CancellationToken ct)
    {
        if(!request.Confirm||request.ConsultationId==Guid.Empty)throw new OrcamentoException(422,"Confirme os dados oficiais e informe a consulta emitida para o seu usuário.");
        QuoteCalculator.CheckText(request.Reason,"Justificativa da importação",1000);if(string.IsNullOrWhiteSpace(request.Reason))throw new OrcamentoException(422,"Justifique a importação/vinculação do cliente.");
        AdminConsultationRow ticket;AdminClientProof proof;(int Id,string Name) actor;
        await using(var read=await db.OpenConnectionAsync(ct))
        {
            actor=await Actor(read,null,ct);ticket=await AdminTicket(read,null,request.ConsultationId,ct);
            if(ticket.ActorId!=actor.Id)throw new OrcamentoException(403,"Consulta vinculada a outro usuário.");
            proof=OrcamentoJson.Read<AdminClientProof>(ticket.Document);
            if(ticket.Used!=0)return await CompletedAdminImport(read,null,proof.Candidate,ct);
            if(ticket.Expires<=Now)throw new OrcamentoException(409,"Consulta expirada. Consulte novamente antes de importar.");
        }
        var fetched=await directory.GetAsync(proof.Candidate.CodigoAdmin,ct);
        var current=fetched is null?null:AdminClientIdentity.Validate(fetched);
        if(current is null||QuoteEmailSourceValidation.Hash(current)!=proof.Fingerprint)
            throw new OrcamentoException(409,"O cadastro oficial mudou desde a consulta. Revise os dados atuais antes de importar.");
        await using var c=await db.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);actor=await Actor(c,tx,ct);
        ticket=await AdminTicket(c,tx,request.ConsultationId,ct);
        if(ticket.ActorId!=actor.Id)throw new OrcamentoException(403,"Consulta de outro usuário.");
        if(ticket.Used!=0)return await CompletedAdminImport(c,tx,proof.Candidate,ct);
        if(ticket.Expires<=Now)throw new OrcamentoException(409,"Consulta expirada durante a reconferência.");
        var resolution=await CheckAdminIdentity(c,tx,current,ct);
        if(resolution.State=="encontrado_local"&&!resolution.CanImport)
        {
            await MarkAdminTicket(c,tx,request,proof,ct);await tx.CommitAsync(ct);return new(current.CodigoAdmin,current.CodigoAdmin,false);
        }
        if(!resolution.CanImport)throw new OrcamentoException(409,resolution.Message);
        await MarkAdminTicket(c,tx,request,proof,ct);
        var existing=await c.QuerySingleOrDefaultAsync<AdminLocalCandidateRow>(new CommandDefinition("SELECT CODCLIENTE AS Id,NOME AS Name,STATUS AS Status FROM CLIENTES WHERE CODCLIENTE=@id",new{id=current.CodigoAdmin},tx,cancellationToken:ct));
        var created=existing is null;
        if(created)
        {
            if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_FIELDS rf JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME=rf.RDB$FIELD_SOURCE WHERE rf.RDB$RELATION_NAME='CLIENTES' AND rf.RDB$FIELD_NAME NOT IN ('CODCLIENTE','NOME','STATUS') AND COALESCE(rf.RDB$NULL_FLAG,f.RDB$NULL_FLAG,0)=1 AND rf.RDB$DEFAULT_SOURCE IS NULL AND f.RDB$DEFAULT_SOURCE IS NULL AND f.RDB$COMPUTED_SOURCE IS NULL",transaction:tx,cancellationToken:ct))>0)
                throw new OrcamentoException(409,"CLIENTES possui campos obrigatórios ainda não mapeados. Revise o DDL antes de importar; nenhum valor será inventado.");
            var metadata=await c.QuerySingleOrDefaultAsync<AdminClientColumn>(new CommandDefinition("SELECT rf.RDB$IDENTITY_TYPE AS IdentityType,f.RDB$FIELD_TYPE AS FieldType,f.RDB$CHARACTER_LENGTH AS MaxLength FROM RDB$RELATION_FIELDS rf JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME=rf.RDB$FIELD_SOURCE WHERE rf.RDB$RELATION_NAME='CLIENTES' AND rf.RDB$FIELD_NAME='CODCLIENTE'",transaction:tx,cancellationToken:ct));
            if(metadata?.FieldType!=8)throw new OrcamentoException(409,"CODCLIENTE não é INTEGER no esquema efetivo. Revise o DDL antes de importar.");
            var max=await c.ExecuteScalarAsync<int?>(new CommandDefinition("SELECT f.RDB$CHARACTER_LENGTH FROM RDB$RELATION_FIELDS rf JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME=rf.RDB$FIELD_SOURCE WHERE rf.RDB$RELATION_NAME='CLIENTES' AND rf.RDB$FIELD_NAME='NOME'",transaction:tx,cancellationToken:ct));
            if(max is null||current.Name.Length>max)throw new OrcamentoException(409,"Nome oficial excede o tamanho do campo local. Não será truncado; revise o cadastro/esquema.");
            var overriding=metadata.IdentityType==0?" OVERRIDING SYSTEM VALUE":"";
            var actual=await c.ExecuteScalarAsync<int>(new CommandDefinition($"INSERT INTO CLIENTES(CODCLIENTE,NOME,STATUS){overriding} VALUES(@id,@name,1) RETURNING CODCLIENTE",new{id=current.CodigoAdmin,name=current.Name},tx,cancellationToken:ct));
            if(actual!=current.CodigoAdmin)throw new OrcamentoException(409,"Um trigger/gerador alterou CODCLIENTE. Importação desfeita; não alterar sequências automaticamente.");
            var stored=await c.QuerySingleAsync<AdminLocalCandidateRow>(new CommandDefinition("SELECT CODCLIENTE AS Id,NOME AS Name,STATUS AS Status FROM CLIENTES WHERE CODCLIENTE=@id",new{id=actual},tx,cancellationToken:ct));
            if(stored.Name!=current.Name||stored.Status!=1)throw new OrcamentoException(409,"Triggers alteraram o cadastro importado. Importação desfeita para revisão do esquema.");
        }
        await c.ExecuteAsync(new CommandDefinition("INSERT INTO CLIENTEADMIN(CODCLIENTE,CNPJ,DOCUMENTO,CODUSUARIO,DTHRCRIACAO) VALUES(@id,@cnpj,@json,@actor,@now)",new{id=current.CodigoAdmin,cnpj=current.Cnpj,json=QuoteTextBlob.Json(current),actor=actor.Id,now=Now},tx,cancellationToken:ct));
        await Audit(c,tx,"CLIENTES",current.CodigoAdmin.ToString(System.Globalization.CultureInfo.InvariantCulture),"importar_cliente_admin",ct);
        await tx.CommitAsync(ct);return new(current.CodigoAdmin,current.CodigoAdmin,created);
    }
    private static async Task MarkAdminTicket(DbConnection c,DbTransaction tx,ImportAdminClientRequest request,AdminClientProof proof,CancellationToken ct)
    {
        if(await c.ExecuteAsync(new CommandDefinition("UPDATE CLIENTEADMINCONSULTA SET CONSUMIDA=1,DOCUMENTO=@json WHERE CODCLIENTEADMINCONSULTA=@id AND CONSUMIDA=0",new{id=request.ConsultationId.ToString("D"),json=QuoteTextBlob.Json(proof with {ImportReason=request.Reason})},tx,cancellationToken:ct))!=1)throw new OrcamentoException(409,"Consulta já consumida por outra requisição. Atualize antes de repetir.");
    }
    private static async Task<AdminClientImportResult> CompletedAdminImport(DbConnection c,DbTransaction? tx,AdminClientSnapshot candidate,CancellationToken ct)
    {
        var result=await CheckAdminIdentity(c,tx,candidate,ct);
        if(result.State!="encontrado_local"||result.CanImport)throw new OrcamentoException(409,"O vínculo importado foi alterado. Solicite revisão, sem recadastrar automaticamente.");
        return new(candidate.CodigoAdmin,candidate.CodigoAdmin,false);
    }
    private static async Task<AdminConsultationRow> AdminTicket(DbConnection c,DbTransaction? tx,Guid id,CancellationToken ct)=>
        await c.QuerySingleOrDefaultAsync<AdminConsultationRow>(new CommandDefinition("SELECT CODUSUARIO AS ActorId,DOCUMENTO AS Document,DTHREXPIRACAO AS Expires,CONSUMIDA AS Used FROM CLIENTEADMINCONSULTA WHERE CODCLIENTEADMINCONSULTA=@id",new{id=id.ToString("D")},tx,cancellationToken:ct))??throw new OrcamentoException(404,"Consulta Admin não encontrada.");
    private static async Task<List<AdminIdentityRow>> IdentityRows(DbConnection c,DbTransaction? tx,CancellationToken ct)
    {
        var rows=(await c.QueryAsync<AdminIdentityRow>(new CommandDefinition("SELECT c.CODCLIENTE AS Id,c.NOME AS Name,c.STATUS AS Status,m.CNPJ AS MappedCnpj,m.DOCUMENTO AS MappingDocument,b.DOCUMENTO AS CommercialDocument FROM CLIENTES c LEFT JOIN CLIENTEADMIN m ON m.CODCLIENTE=c.CODCLIENTE LEFT JOIN CLIENTECOMERCIAL b ON b.CODCLIENTE=c.CODCLIENTE",transaction:tx,cancellationToken:ct))).ToList();
        foreach(var row in rows)
        {
            try
            {
                row.FiscalId=row.CommercialDocument is null?null:OrcamentoJson.Read<CommercialClient>(row.CommercialDocument).FiscalId;
                row.Cnpj=AdminClientIdentity.TryCnpj(row.FiscalId);
                row.Mapping=row.MappingDocument is null?null:AdminClientIdentity.Validate(OrcamentoJson.Read<AdminClientSnapshot>(row.MappingDocument));
            }
            catch(Exception ex) when(ex is System.Text.Json.JsonException or OrcamentoException){throw new OrcamentoException(409,"Há documentos locais de identidade inválidos. Solicite revisão; nenhum cadastro será sobrescrito.");}
        }
        return rows;
    }
    private static bool Verified(AdminIdentityRow r)=>r.Status==1&&r.Mapping is {Active:true} m&&m.CodigoAdmin==r.Id&&m.Cnpj==r.MappedCnpj?.Trim()&&AdminClientIdentity.NameKey(m.Name)==AdminClientIdentity.NameKey(r.Name)
        &&(string.IsNullOrWhiteSpace(r.FiscalId)||r.Cnpj==m.Cnpj);
    private static async Task<List<AdminLocalCandidate>> AdminLocalCandidates(DbConnection c,DbTransaction? tx,CancellationToken ct)=>
        (await IdentityRows(c,tx,ct)).Select(r=>new AdminLocalCandidate(r.Id,r.Name,r.Mapping?.Cnpj??r.Cnpj,Verified(r))).ToList();
    private static async Task<AdminClientResolution> CheckAdminIdentity(DbConnection c,DbTransaction? tx,AdminClientSnapshot candidate,CancellationToken ct)
    {
        AdminClientResolution Conflict(string reason)=>new("conflito",reason,candidate);
        if(candidate.CodigoAdmin==1)return Conflict("O código 1 é reservado ao cliente padrão. Não substituir nem reutilizar automaticamente.");
        if(!candidate.Active)return new("inativo","O cliente está inativo no Admin. Não importar nem reativar automaticamente.",candidate);
        var rows=await IdentityRows(c,tx,ct);var current=rows.SingleOrDefault(r=>r.Id==candidate.CodigoAdmin);
        if(rows.Any(r=>r.Id!=candidate.CodigoAdmin&&(r.Cnpj==candidate.Cnpj||r.MappedCnpj?.Trim()==candidate.Cnpj)))return Conflict("O CNPJ já pertence a outro código local. Revisão humana necessária; não fundir ou renumerar.");
        if(rows.Any(r=>r.Id!=candidate.CodigoAdmin&&AdminClientIdentity.NameKey(r.Name)==AdminClientIdentity.NameKey(candidate.Name)))return Conflict("Já existe nome igual em outro código local. Não vincular por nome; confira a identidade antes de cadastrar.");
        if(current is not null)
        {
            if(current.Status!=1)return Conflict("O cadastro local está inativo. Não reativar automaticamente.");
            if(AdminClientIdentity.NameKey(current.Name)!=AdminClientIdentity.NameKey(candidate.Name))return Conflict("O código local está ocupado por nome divergente. Não sobrescrever nem presumir equivalência com o Admin.");
            if(!string.IsNullOrWhiteSpace(current.FiscalId)&&current.Cnpj!=candidate.Cnpj)return Conflict("O documento comercial local é diferente ou não comprova este CNPJ. Solicite revisão.");
            if(current.Mapping is not null)
            {
                if(!Verified(current)||current.Mapping.Cnpj!=candidate.Cnpj)return Conflict("O vínculo Admin existente diverge do cadastro oficial/local. Não substituir automaticamente.");
                return new("encontrado_local","Cliente local com vínculo Admin verificado.",candidate,current.Id);
            }
            if(current.Cnpj!=candidate.Cnpj)return Conflict("Código legado sem CNPJ independente para comprovar a identidade. Nome/código iguais, isoladamente, não autorizam vinculação.");
            return new("encontrado_local","CNPJ e nome local correspondem ao Admin. Um humano pode registrar o vínculo sem alterar o cadastro existente.",candidate,current.Id,true);
        }
        var size=await c.ExecuteScalarAsync<int?>(new CommandDefinition("SELECT f.RDB$CHARACTER_LENGTH FROM RDB$RELATION_FIELDS rf JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME=rf.RDB$FIELD_SOURCE WHERE rf.RDB$RELATION_NAME='CLIENTES' AND rf.RDB$FIELD_NAME='NOME'",transaction:tx,cancellationToken:ct));
        if(size is null||candidate.Name.Length>size)return Conflict("Nome oficial incompatível com o tamanho do campo local. Não truncar; revisar o esquema/cadastro.");
        return new("encontrado_admin","Cliente oficial ausente localmente. Importação depende da confirmação humana.",candidate,CanImport:true);
    }
    private sealed class AdminIdentityRow
    {
        public int Id{get;set;}public string Name{get;set;}="";public int? Status{get;set;}
        public string? MappedCnpj{get;set;}public string? MappingDocument{get;set;}public string? CommercialDocument{get;set;}
        public string? FiscalId{get;set;}public string? Cnpj{get;set;}public AdminClientSnapshot? Mapping{get;set;}
    }
    private sealed class AdminConsultationRow {public int ActorId{get;set;}public string Document{get;set;}="";public DateTime Expires{get;set;}public int Used{get;set;}}
    private sealed class AdminLocalCandidateRow {public int Id{get;set;}public string Name{get;set;}="";public int? Status{get;set;}}
    private sealed class AdminClientColumn {public short? IdentityType{get;set;}public short FieldType{get;set;}public int? MaxLength{get;set;}}
}
