using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MdwConteudos.Api.Modules.Auditoria;

public sealed record AuditEvent(
    string Id, string OccurredAtUtc, string Module, string Action, string Result,
    string? EntityType, string? EntityId, string? EntityName, string Summary,
    int? ActorId, string? ActorName, string ActorKind, string Origin,
    string? CorrelationId, string? OperationId);

public sealed record AuditChange(string Field, object? Before, object? After);
public sealed record AuditDetail(
    string Id, string OccurredAtUtc, string Module, string Action, string Result,
    string? EntityType, string? EntityId, string? EntityName, string Summary,
    int? ActorId, string? ActorName, string ActorKind, string Origin,
    string? CorrelationId, string? OperationId,
    string? RequestMethod, string? RequestPath, string? IpAddress, string? ReasonCode,
    IReadOnlyList<AuditChange> Changes);
public sealed record AuditPage(IReadOnlyList<AuditEvent> Data, string? NextCursor);
public sealed class AuditQuery
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int? ActorId { get; set; }
    public string? Module { get; set; }
    public string? Action { get; set; }
    public string? Result { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? Cursor { get; set; }
    public int Take { get; set; } = 50;
}

public static class AuditCatalog
{
    public static readonly string[] Modules = ["autenticacao", "usuarios", "permissoes", "scripts", "paineis", "relatorios", "variaveis", "formulas", "referencias", "pacotes", "cadastros", "orcamentos"];
    public static readonly string[] Actions = ["criar", "editar", "excluir", "ativar", "inativar", "aprovar", "reprovar", "vincular", "desvincular", "importar", "upload", "remover_anexo", "login", "redefinir_senha", "alterar_senha", "download", "exportar", "acesso_negado", "analise", "solicitar_aprovacao", "devolver", "enviar", "aceitar", "confirmar_pagamento", "encaminhar", "recusar", "cancelar", "arquivar", "nova_revisao", "analisar_ia", "cancelar_ia", "aplicar_ia", "resultado_ia", "importar_email", "confirmar_email", "email_autorizado", "email_revogado", "email_iniciado", "email_enviado", "email_incerto"];
    public static readonly string[] Results = ["sucesso", "falha", "negado", "iniciado"];

    public static string Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
    public static object? SafeValue(string? value, string type) => value is null ? null : type switch
    {
        "number" => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.Parse(value, CultureInfo.InvariantCulture),
        "boolean" => value == "TRUE" || value == "1",
        _ => value
    };
}

internal sealed class AuditRow
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Module { get; set; } = "";
    public string Action { get; set; } = "";
    public string Result { get; set; } = "";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? EntityName { get; set; }
    public string Summary { get; set; } = "";
    public int? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string ActorKind { get; set; } = "anonimo";
    public string Origin { get; set; } = "web";
    public string? CorrelationId { get; set; }
    public string? OperationId { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public string? IpAddress { get; set; }
    public string? ReasonCode { get; set; }
    public AuditEvent Event() => new(Id.ToString(CultureInfo.InvariantCulture), AuditCatalog.Utc(OccurredAtUtc), Module, Action, Result,
        EntityType, EntityId, EntityName, Summary, ActorId, ActorName, ActorKind, Origin, CorrelationId, OperationId);
    public AuditDetail Detail(IReadOnlyList<AuditChange> changes) => new(Id.ToString(CultureInfo.InvariantCulture), AuditCatalog.Utc(OccurredAtUtc), Module, Action, Result,
        EntityType, EntityId, EntityName, Summary, ActorId, ActorName, ActorKind, Origin, CorrelationId, OperationId,
        RequestMethod, RequestPath, IpAddress, ReasonCode, changes);
}
