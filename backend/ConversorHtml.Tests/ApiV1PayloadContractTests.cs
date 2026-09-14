using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Modules.ApiPublica;

namespace ConversorHtml.Tests;

public class ApiV1PayloadContractTests
{
    public static IEnumerable<object[]> PythonEcodoCases => ApiV1TestSupport.Cases("ecodoppler");

    [Theory]
    [MemberData(nameof(PythonEcodoCases))]
    public void Ecodo_matches_executed_Python_reference(string name, string json)
    {
        using var fixture = JsonDocument.Parse(json);
        var item = fixture.RootElement;
        var rows = item.GetProperty("rows").EnumerateArray().Select(ApiV1TestSupport.Row).ToArray();
        var actual = EcodopplerBuilder.Build(rows);
        ApiV1TestSupport.EqualJson(JsonNode.Parse(item.GetProperty("expected").GetRawText()), JsonSerializer.SerializeToNode(actual));
    }

    [Fact]
    public void Python_microseconds_and_workflow_key_are_preserved()
    {
        foreach (var fixture in ApiV1TestSupport.Reference.GetProperty("dates").EnumerateArray())
        {
            var date = new DateTime(2026, 9, 8, 9, 10, 11).AddTicks(fixture.GetProperty("microseconds").GetInt64() * 10);
            Assert.Equal(fixture.GetProperty("iso").GetString(), ApiV1Contract.DateTimeIso(date));
            var item = new Dictionary<string, object?> { ["codscriptlaudo"] = 42, ["data_verificacao"] = ApiV1Contract.FormatDate(date) };
            Assert.Equal(fixture.GetProperty("workflow_key").GetString(), ApiV1Contract.WorkflowKey(item));
            // .NET's extra sub-microsecond digit must never change an n8n dedup key.
            Assert.Equal(fixture.GetProperty("iso").GetString(), ApiV1Contract.DateTimeIso(date.AddTicks(9)));
        }
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void Naive_wire_dates_do_not_acquire_timezone_suffixes(DateTimeKind kind) =>
        Assert.Equal("2026-09-08T09:10:11", ApiV1Contract.FormatDate(new DateTime(2026, 9, 8, 9, 10, 11, kind)));

    [Fact]
    public void FormatDate_preserves_null_date_only_and_explicit_offset()
    {
        Assert.Null(ApiV1Contract.FormatDate(null));
        Assert.Null(ApiV1Contract.FormatDate(DBNull.Value));
        Assert.Equal("2026-09-08", ApiV1Contract.FormatDate(new DateOnly(2026, 9, 8)));
        Assert.Equal("2026-09-08T09:10:11.100000-03:00",
            ApiV1Contract.FormatDate(new DateTimeOffset(2026, 9, 8, 9, 10, 11, TimeSpan.FromHours(-3)).AddMilliseconds(100)));
        Assert.Null(ApiV1Contract.WorkflowKey(new Dictionary<string, object?> { ["codscriptlaudo"] = 42 }));
        Assert.Null(ApiV1Contract.WorkflowKey(new Dictionary<string, object?> { ["codscriptlaudo"] = 42, ["data_verificacao"] = "" }));
    }

    [Fact]
    public void Sistema_info_projects_Firebird_uppercase_columns_and_omits_total_from_envelope()
    {
        dynamic row = new ExpandoObject();
        row.ESPECIALIDADE = "Cardiologia";
        row.TOTAL = 7L;
        var specialities = ApiV1Contract.SistemaEspecialidades(new dynamic[] { row });
        var data = new { variaveis_por_especialidade = specialities, versao_api = "1.0" };
        var now = new DateTime(2026, 9, 8, 9, 10, 11);
        var actual = ApiV1Contract.Ok(data, now: now);
        ApiV1TestSupport.EqualJson(JsonNode.Parse("""
            {"success":true,"data":{"variaveis_por_especialidade":[{"especialidade":"Cardiologia","total":7}],"versao_api":"1.0"},"timestamp":"2026-09-08T09:10:11"}
            """), JsonSerializer.SerializeToNode(actual));
        var collection = ApiV1Contract.Ok(specialities, specialities.Count, now);
        Assert.Equal(1, collection["total"]);
        Assert.False(actual.ContainsKey("total"));
    }

    [Theory]
    [InlineData("Normal", "normal", "default")]
    [InlineData("Leve", "leve", "default")]
    [InlineData("Moderado", "moderado", "moderated")]
    [InlineData("Grave", "grave", "default")]
    public void Modern_client_mapping_and_comments_stay_separate(string classification, string modernZone, string legacyZone)
    {
        var fixture = ApiV1TestSupport.Reference.GetProperty("ecodoppler").EnumerateArray()
            .First(item => item.GetProperty("name").GetString() == "classification-" + classification);
        var rows = fixture.GetProperty("rows").EnumerateArray().Select(ApiV1TestSupport.Row).ToArray();
        var comments = new Dictionary<string, Dictionary<string, string>> { ["VR_AO"] = new() { ["A"] = " Comentário " } };
        var modern = ClienteNormalidadesBuilder.Build(rows, comments);
        Assert.True(modern["VR_AO"]["M"].ContainsKey(modernZone));
        Assert.Single(modern["VR_AO"]["M"]);
        Assert.Equal("Comentário", modern["VR_AO"]["_comentario_texto"]["texto"]);
        Assert.True(EcodopplerBuilder.Build(rows)["VR_AO"]["M"].ContainsKey(legacyZone));
    }

    [Fact]
    public async Task Ecodo_invalid_reference_preserves_error_without_database_access()
    {
        var service = new ApiPublicaService(new ApiV1NoDatabase(), Options.Create(new ApiPartnerOptions()), null!);
        var result = Assert.IsType<BadRequestObjectResult>(await service.GetEcodopplerAsync(0, default));
        ApiV1TestSupport.EqualJson(JsonNode.Parse("""
            {"success":false,"error":"Parâmetro referencia inválido","message":"Informe um código de referência inteiro ≥ 1."}
            """), JsonSerializer.SerializeToNode(result.Value));
    }

    [Theory]
    [InlineData(" 2 ", null, "Use ativo=1 para relatórios ativos ou ativo=0 para inativos")]
    [InlineData(null, " true ", "Use multiselecao=1 (com multiseleção) ou multiselecao=0 (sem)")]
    public async Task Report_filter_errors_keep_legacy_messages(string? active, string? multiselect, string message)
    {
        var service = new ApiPublicaService(new ApiV1NoDatabase(), Options.Create(new ApiPartnerOptions()), null!);
        var result = Assert.IsType<BadRequestObjectResult>(await service.GetRelatoriosAsync(active, multiselect, null, null, null, null, default));
        Assert.Equal(message, JsonSerializer.SerializeToElement(result.Value).GetProperty("message").GetString());
    }

    [Fact]
    public void Date_format_is_not_affected_by_current_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            Assert.Equal("2026-09-08T09:10:11.123456", ApiV1Contract.DateTimeIso(new DateTime(2026, 9, 8, 9, 10, 11).AddTicks(1234560)));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
