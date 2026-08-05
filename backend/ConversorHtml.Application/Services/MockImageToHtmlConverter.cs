using System.Text;
using ConversorHtml.Application.Interfaces;

namespace ConversorHtml.Application.Services;

public class MockImageToHtmlConverter : IImageToHtmlConverter
{
    public async Task<string> ConvertAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default)
    {
        await imageStream.CopyToAsync(Stream.Null, cancellationToken);

        var safeName = Path.GetFileNameWithoutExtension(fileName);
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        return BuildTemplate(safeName, timestamp);
    }

    private static string BuildTemplate(string sourceName, string timestamp)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<style>");
        sb.AppendLine("    .laudo-layout { display: flex; flex-wrap: wrap; gap: 20px; font-family: 'Segoe UI', Arial, sans-serif; }");
        sb.AppendLine("    .laudo-coluna { flex: 1 1 300px; min-width: 280px; }");
        sb.AppendLine("    .title { font-size: 18px; background-color: #e6e6e6; border-radius: 5px; padding: 5px 0 5px 5px; margin-top: 10px; font-weight: bold; color: #242424; width: 100%; max-width: 620px; }");
        sb.AppendLine("    .campoMedida { border-radius: 5px; border: 1px solid #e3e3e3; width: 63px; height: 30px; margin: 5px 2px 0 0; padding-right: 6px; font-size: 16px; text-align: end; }");
        sb.AppendLine("    .campoMedida[type=number]::-webkit-inner-spin-button, .campoMedida[type=number]::-webkit-outer-spin-button { -webkit-appearance: none; margin: 0; }");
        sb.AppendLine("    .unidadeMedida { font-size: 14px; width: 45px; text-align: left; color: #333; }");
        sb.AppendLine("    .referencia { font-size: 14px; text-align: left; color: #484848; }");
        sb.AppendLine("    .descricaoMedida span { font-weight: bold; color: #333; }");
        sb.AppendLine("    .campoSomenteLeitura { background-color: #f0f0f0; color: #666; }");
        sb.AppendLine("    .laudo-linha { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; }");
        sb.AppendLine("    .laudo-campo-wrapper { display: flex; align-items: center; gap: 4px; }");
        sb.AppendLine("    #CollapseColunaEsquerda, #CollapseColunaDireita { border: none; }");
        sb.AppendLine("    #CollapseColunaEsquerda > div, #CollapseColunaDireita > div { border: none; }");
        sb.AppendLine("</style>");
        sb.AppendLine();
        sb.AppendLine("<div id=\"containerHtml\">");
        sb.AppendLine("    <div class=\"d-flex laudo-layout\">");
        sb.AppendLine("        <div id=\"CollapseColunaEsquerda\" class=\"laudo-coluna\" ats=\"imprimir\" percent=\"50\" indexImpressao=\"1\">");
        sb.AppendLine($"            <div class=\"title\">DADOS DO PACIENTE — {sourceName}</div>");
        sb.AppendLine(GenerateField("VR_ALTURA", "Altura", "cm", "100 - 220"));
        sb.AppendLine(GenerateField("VR_PESO", "Peso", "kg", "30 - 200"));
        sb.AppendLine(GenerateReadOnlyField("VR_IMC", "IMC", "kg/m²", "18.5 - 24.9"));
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div id=\"CollapseColunaDireita\" class=\"laudo-coluna\" ats=\"imprimir\" percent=\"50\" indexImpressao=\"2\">");
        sb.AppendLine("            <div class=\"title\">CÂMARAS ESQUERDAS</div>");
        sb.AppendLine(GenerateField("VR_ANEL_AORTICO", "Anel aórtico", "mm", "19 - 26"));
        sb.AppendLine(GenerateField("VR_ARCO_AORTICO", "Arco aórtico", "mm", "20 - 35"));
        sb.AppendLine(GenerateField("VR_VIA_SAIDA_VE", "Via de saída do VE", "mm", "10 - 21"));
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("</div>");
        sb.AppendLine();
        sb.AppendLine("<script>");
        sb.AppendLine("    let dadosPaciente = {};");
        sb.AppendLine();
        sb.AppendLine("    function iniciarFuncoes() {");
        sb.AppendLine("        try {");
        sb.AppendLine("            obterDadosInterface();");
        sb.AppendLine("            configurarCalculos();");
        sb.AppendLine($"            console.log('Script mock gerado em {timestamp} a partir de {sourceName}');");
        sb.AppendLine("        } catch (error) {");
        sb.AppendLine("            console.error('Erro na inicialização:', error);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    function obterDadosInterface() {");
        sb.AppendLine("        try {");
        sb.AppendLine("            const altura = parseFloat(document.getElementById('VR_ALTURA')?.value) || 0;");
        sb.AppendLine("            const peso = parseFloat(document.getElementById('VR_PESO')?.value) || 0;");
        sb.AppendLine("            dadosPaciente = { altura: altura, peso: peso };");
        sb.AppendLine("        } catch (error) {");
        sb.AppendLine("            console.error('Erro na função obterDadosInterface', error);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    function configurarCalculos() {");
        sb.AppendLine("        try {");
        sb.AppendLine("            const campoAltura = document.getElementById('VR_ALTURA');");
        sb.AppendLine("            const campoPeso = document.getElementById('VR_PESO');");
        sb.AppendLine("            if (campoAltura) campoAltura.setAttribute('oninput', 'calcularIMC()');");
        sb.AppendLine("            if (campoPeso) campoPeso.setAttribute('oninput', 'calcularIMC()');");
        sb.AppendLine("        } catch (error) {");
        sb.AppendLine("            console.error('Erro na função configurarCalculos', error);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    function calcularIMC() {");
        sb.AppendLine("        try {");
        sb.AppendLine("            const peso = parseFloat(document.getElementById('VR_PESO')?.value) || 0;");
        sb.AppendLine("            const altura = parseFloat(document.getElementById('VR_ALTURA')?.value) || 0;");
        sb.AppendLine("            if (peso > 0 && altura > 0) {");
        sb.AppendLine("                const imc = peso / Math.pow(altura / 100, 2);");
        sb.AppendLine("                const campoImc = document.getElementById('VR_IMC');");
        sb.AppendLine("                if (campoImc) campoImc.value = imc.toFixed(1);");
        sb.AppendLine("            }");
        sb.AppendLine("        } catch (error) {");
        sb.AppendLine("            console.error('Erro na função calcularIMC', error);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    iniciarFuncoes();");
        sb.AppendLine("</script>");

        return sb.ToString();
    }

    private static string GenerateField(string id, string label, string unit, string reference)
    {
        return $"""
            <div class="laudo-linha">
                <div class="descricaoMedida">
                    <span for="{id}">{label}</span>
                </div>
                <div class="laudo-campo-wrapper">
                    <input id="{id}" class="campoMedida" type="number" step="any">
                    <span class="unidadeMedida">{unit}</span>
                    <span for="{id}" class="referencia ml-2" id="{id}_NORMALIDADE">{reference}</span>
                    <span for="{id}" id="{id}_COMENTARIONORMALIDADE"></span>
                </div>
            </div>
""";
    }

    private static string GenerateReadOnlyField(string id, string label, string unit, string reference)
    {
        return $"""
            <div class="laudo-linha">
                <div class="descricaoMedida">
                    <span for="{id}">{label}</span>
                </div>
                <div class="laudo-campo-wrapper">
                    <input id="{id}" class="campoMedida campoSomenteLeitura" type="number" step="any" disabled>
                    <span class="unidadeMedida">{unit}</span>
                    <span for="{id}" class="referencia ml-2" id="{id}_NORMALIDADE">{reference}</span>
                    <span for="{id}" id="{id}_COMENTARIONORMALIDADE"></span>
                </div>
            </div>
""";
    }
}
