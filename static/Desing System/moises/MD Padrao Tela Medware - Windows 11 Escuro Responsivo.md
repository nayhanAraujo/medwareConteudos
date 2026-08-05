# Guia visual MEDWARE — Windows 11 escuro e responsivo para WinForms

## Fonte única de verdade

Este documento consolida:

- o conteúdo do guia visual MEDWARE original;
- as decisões aprovadas nos módulos **Meus Exames**, **Uploader**, **WebCam** e **Login**;
- o tema escuro inspirado no Windows 11;
- os padrões de ícones, botões, campos, cartões e tabelas;
- a adaptação automática de janelas fixas para monitores HD;
- exemplos de implementação compatíveis com C# Windows Forms.

Quando houver conflito com o guia antigo, prevalece o padrão escuro responsivo deste documento.

---

## 1. Objetivos

- Modernizar o produto sem perder a identidade de aplicativo desktop.
- Manter a implementação viável em Windows Forms.
- Preservar regras de negócio, eventos, nomes de controles e fluxo de trabalho.
- Criar consistência entre módulos clínicos, administrativos e multimídia.
- Organizar a informação por prioridade e contexto.
- Eliminar grandes áreas vazias e blocos de cor sem função.
- Garantir boa leitura em HD, Full HD e escalas de 100% a 150%.
- Impedir que janelas fixas sejam cortadas pela resolução ou pela barra de tarefas.

### Princípios

1. Conteúdo antes da decoração.
2. Fundo geral escuro e superfícies elevadas discretas.
3. Azul identifica ações principais, foco e seleção.
4. Verde, amarelo e vermelho possuem significado semântico.
5. Controles compactos e adequados ao desktop.
6. Composição com controles nativos sempre que possível.
7. Ícones devem ajudar a reconhecer comandos, não servir apenas como enfeite.
8. O layout deve funcionar sem depender de uma resolução específica.

---

## 2. Tema atual aprovado: Windows 11 escuro

### Paleta estrutural

| Token | Cor | Uso |
|---|---:|---|
| `AppBackground` | `#202020` | Fundo geral do formulário |
| `Surface` | `#2B2B2B` | Cartões, painéis, tabelas e barras |
| `SurfaceSecondary` | `#252525` | Menus, cabeçalhos e áreas secundárias |
| `SurfaceElevated` | `#323232` | Campos, hover e superfícies elevadas |
| `Border` | `#454545` | Bordas de cartões e controles |
| `Divider` | `#3A3A3A` | Separadores e linhas internas |
| `TextPrimary` | `#F5F5F5` | Títulos e conteúdo principal |
| `TextSecondary` | `#C7C7C7` | Subtítulos e informações auxiliares |
| `TextMuted` | `#9A9A9A` | Placeholder, estado vazio e desabilitado |

### Ações e estados

| Token | Cor | Uso |
|---|---:|---|
| `Primary` | `#0F6CBD` | Ação principal, seleção e foco |
| `PrimaryHover` | `#1A86D9` | Mouse sobre ação principal |
| `PrimaryPressed` | `#0C3B5E` | Ação principal pressionada |
| `PrimaryLight` | `#153F59` | Seleção escura e foco leve |
| `PrimaryAccent` | `#60CDFF` | Contorno de foco e pequenos destaques |
| `Success` | `#107C10` | Salvar, concluir e confirmar |
| `SuccessHover` | `#168B16` | Mouse sobre ação positiva |
| `Warning` | `#E6A700` | Atenção, espera e pendência |
| `Danger` | `#C42B1C` | Cancelar, sair, fechar e excluir |
| `DangerHover` | `#D13438` | Mouse sobre ação destrutiva |
| `Info` | `#2589D8` | Envio, processamento e informação |

### Regras de cor

- Não usar preto absoluto `#000000` em toda a janela; usar `#202020` para reduzir contraste excessivo.
- Áreas de vídeo ou imagem podem usar `#101010`.
- Não usar cor saturada como fundo de uma seção inteira.
- Usar azul para ação, verde para confirmação e vermelho para cancelamento.
- Texto normal deve permanecer branco ou cinza-claro.
- Logotipos sobre fundo escuro devem usar versão branca ou de alto contraste.

### Classe de tokens

```csharp
internal static class MedwareDarkTheme
{
    internal static readonly Color AppBackground =
        ColorTranslator.FromHtml("#202020");
    internal static readonly Color Surface =
        ColorTranslator.FromHtml("#2B2B2B");
    internal static readonly Color SurfaceSecondary =
        ColorTranslator.FromHtml("#252525");
    internal static readonly Color SurfaceElevated =
        ColorTranslator.FromHtml("#323232");
    internal static readonly Color Border =
        ColorTranslator.FromHtml("#454545");
    internal static readonly Color Divider =
        ColorTranslator.FromHtml("#3A3A3A");
    internal static readonly Color TextPrimary =
        ColorTranslator.FromHtml("#F5F5F5");
    internal static readonly Color TextSecondary =
        ColorTranslator.FromHtml("#C7C7C7");
    internal static readonly Color TextMuted =
        ColorTranslator.FromHtml("#9A9A9A");
    internal static readonly Color Primary =
        ColorTranslator.FromHtml("#0F6CBD");
    internal static readonly Color PrimaryLight =
        ColorTranslator.FromHtml("#153F59");
    internal static readonly Color PrimaryAccent =
        ColorTranslator.FromHtml("#60CDFF");
    internal static readonly Color Success =
        ColorTranslator.FromHtml("#107C10");
    internal static readonly Color Danger =
        ColorTranslator.FromHtml("#C42B1C");
}
```

---

## 3. Perfil claro legado

O guia original utilizava o perfil abaixo. Ele pode ser mantido em telas que ainda não foram migradas, mas não deve ser misturado parcialmente com o tema escuro.

| Token | Cor |
|---|---:|
| `AppBackground` | `#F3F6FA` |
| `Surface` | `#FFFFFF` |
| `SurfaceSecondary` | `#F8FAFC` |
| `Border` | `#D9E2EC` |
| `Divider` | `#E7EDF3` |
| `TextPrimary` | `#243447` |
| `TextSecondary` | `#5E6E7E` |
| `TextMuted` | `#8493A3` |
| `PrimaryLight` | `#EAF3FB` |

Uma tela deve usar o perfil claro completo ou o perfil escuro completo. Não combinar cartões brancos, fundo preto e cabeçalhos claros sem uma decisão específica de contraste.

---

## 4. Tipografia

Usar **Segoe UI** em toda a aplicação.

| Elemento | Tamanho Full HD | Peso | Cor |
|---|---:|---|---|
| Título da tela | 20–22 pt | Bold/Semibold | `TextPrimary` |
| Subtítulo | 10–11 pt | Regular | `TextSecondary` |
| Título de seção | 11–12 pt | Semibold | Branco |
| Rótulo de campo | 9,5–10 pt | Regular | `TextSecondary` |
| Texto normal | 9,5–10 pt | Regular | `TextPrimary` |
| Texto auxiliar | 9 pt | Regular | `TextMuted` |
| Indicador numérico | 22–24 pt | Bold | `TextPrimary` |

```csharp
form.Font = new Font("Segoe UI", 10F);
lblTitulo.Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold);
lblSecao.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
lblAuxiliar.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
```

### Perfil HD

Ao aplicar o perfil compacto de 85%, reduzir também as fontes explícitas. O WinForms pode reduzir os controles sem reduzir fontes atribuídas manualmente, causando textos quebrados.

- Tamanho mínimo recomendado: `8.25 pt`.
- Rótulos como `WebService`, `Unidade`, `Usuário` e `Telefone` devem permanecer em uma linha.
- Não reduzir apenas a janela; reduzir controles, fontes, paddings e margens proporcionalmente.

---

## 5. Espaçamento e medidas

Usar múltiplos de 4 pixels.

| Token | Medida | Uso |
|---|---:|---|
| `SpaceXS` | 4 px | Ícone e texto próximos |
| `SpaceS` | 8 px | Elementos internos compactos |
| `SpaceM` | 12 px | Campos relacionados |
| `SpaceL` | 16 px | Padding interno mínimo de cartão |
| `SpaceXL` | 24 px | Blocos principais |
| `SpaceXXL` | 32 px | Margem externa do formulário |
| `SpaceDialog` | 40 px | Margem ideal em diálogos grandes, quando houver espaço |

Medidas Full HD:

- Margem externa: 32 a 40 px.
- Distância entre cartões: 16 px.
- Padding de cartão: 16 a 20 px.
- Campos: 32 a 38 px de altura.
- Botões: 40 px de altura.
- Cabeçalho de grade: 42 px.
- Linhas de grade: 44 px.
- Ícones principais: 32 × 32 px.
- Raio visual: 6 a 8 px.

No perfil HD, todas as medidas das janelas fixas são reduzidas para 85%, preservando a proporção.

---

## 6. Estrutura padrão da tela

1. Barra de título do Windows.
2. Menu principal, se necessário.
3. Cabeçalho com título, descrição e marca.
4. Barra de comandos.
5. Contexto, filtros ou indicadores.
6. Conteúdo principal ocupando o espaço restante.
7. Estado vazio dentro do próprio conteúdo.
8. Barra de status.

```text
Form
└── TableLayoutPanel rootLayout (Dock = Fill)
    ├── MenuStrip                       (opcional)
    ├── Panel headerPanel
    ├── ToolStrip commandBar            (opcional)
    ├── Panel filterCard                (opcional)
    ├── Panel contentCard               (SizeType.Percent, 100)
    │   ├── Label sectionTitle
    │   └── DataGridView / SplitContainer / UserControl
    └── StatusStrip statusBar           (opcional)
```

- Usar `Dock = Fill` no layout raiz.
- Usar `TableLayoutPanel` e `FlowLayoutPanel` para reduzir posicionamento absoluto.
- Conteúdo principal deve receber o espaço restante.
- Cabeçalho, filtros e status podem usar tamanho absoluto ou `AutoSize` controlado.

---

## 7. Cabeçalho e marca

O cabeçalho pode conter:

- título da função;
- descrição curta de uma linha;
- marca MEDWARE à direita;
- ação contextual opcional.

Regras:

- Fundo `#202020` ou `#252525`.
- Título branco em Segoe UI Semibold.
- Subtítulo em `TextSecondary`.
- Marca ou logotipo em branco.
- Não colocar fundo azul atrás do logotipo.
- Não deixar rótulos escondidos atrás da imagem.
- Manter área livre ao redor do logotipo.

---

## 8. Cartões e superfícies

### Aparência

- Fundo `Surface` (`#2B2B2B`).
- Borda `Border` (`#454545`) de 1 px.
- Raio visual de 8 px.
- Padding de 16 a 20 px.
- Sombra ausente ou extremamente discreta.
- Título branco e conteúdo secundário cinza-claro.

### Tipos

- **Conteúdo:** filtros, tabelas, detalhes e mídia.
- **Cadastro:** grupo de campos relacionados.
- **Indicador:** ícone, rótulo e valor.
- **Selecionável:** borda `Primary` e fundo `PrimaryLight`.
- **Notificação:** fundo `SurfaceElevated` e pequeno acento `PrimaryAccent`.

### Controle arredondado mínimo

Criar um `RoundedPanel` reutilizável com:

- `FillColor`;
- `BorderColor`;
- `CornerRadius`;
- `DoubleBuffered = true`;
- desenho com `GraphicsPath` e antialiasing.

---

## 9. Campos de entrada

- Fundo `SurfaceElevated`.
- Texto `TextPrimary`.
- Borda `#555555`.
- Altura padrão entre 32 e 38 px.
- Raio visual entre 6 e 8 px.
- Padding horizontal de 8 px.
- Contorno `PrimaryAccent` quando o campo recebe foco.
- Campos desabilitados usam `SurfaceSecondary` e `TextMuted`.

Para WinForms clássico, hospedar `TextBox`, `ComboBox` e `NumericUpDown` dentro de um painel arredondado.

```text
Windows11RoundedFieldHost
├── fundo #323232
├── borda #555555
├── borda em foco #60CDFF
├── raio 6 px
└── campo nativo sem borda
```

Usar `ErrorProvider` para validação próxima ao campo. Não abrir um `MessageBox` para cada erro simples.

---

## 10. Botões

### Regra atual aprovada

- Botões comuns não possuem ícones.
- Texto sempre centralizado horizontal e verticalmente.
- Altura mínima de 40 px.
- Fonte Segoe UI Semibold 10 pt.
- Cantos suavemente arredondados, raio de aproximadamente 7 px.
- Cursor de mão em ações clicáveis.

### Primário

- Fundo `Primary`.
- Texto branco.
- Sem borda.
- Usado para pesquisar, selecionar, imprimir ou executar a ação principal.

### Salvar ou confirmar

- Fundo `Success` (`#107C10`).
- Texto branco.
- Sem ícone.
- Texto centralizado.

### Cancelar, sair ou fechar

- Fundo `Danger` (`#C42B1C`).
- Texto branco.
- Sem ícone.
- Texto centralizado.

### Secundário

- Fundo `Surface`.
- Borda `Border`.
- Texto `TextPrimary`.

### Excluir ou remover

- Pode usar fundo vermelho quando a intenção for explícita.
- Exigir confirmação para ação irreversível.

```csharp
button.Image = null;
button.TextAlign = ContentAlignment.MiddleCenter;
button.TextImageRelation = TextImageRelation.Overlay;
button.FlatStyle = FlatStyle.Flat;
button.Height = Math.Max(button.Height, 40);
```

---

## 11. Ícones

### Padrão atual aprovado

- Ícones de barra em 32 × 32 px.
- Estilo Fluent inspirado no Windows 11.
- Monocromáticos, preferencialmente brancos no tema escuro.
- Formas simples, expressivas e reconhecíveis.
- Fundo transparente.
- O mesmo comando usa o mesmo símbolo em todos os módulos.
- Botões retangulares de formulário não usam ícones.

### Fonte de ícones

Preferir glifos nativos:

1. `Segoe Fluent Icons`;
2. fallback para `Segoe MDL2 Assets`.

Isso evita PNGs borrados, mantém consistência e permite renderização em qualquer tamanho.

```csharp
private static Font CreateIconFont(int size)
{
    Font font = new Font(
        "Segoe Fluent Icons",
        size * 0.62F,
        FontStyle.Regular,
        GraphicsUnit.Pixel);

    if (font.Name.Equals("Segoe Fluent Icons",
        StringComparison.OrdinalIgnoreCase))
        return font;

    font.Dispose();
    return new Font(
        "Segoe MDL2 Assets",
        size * 0.62F,
        FontStyle.Regular,
        GraphicsUnit.Pixel);
}
```

### Mapeamento recomendado

| Ação | Glifo |
|---|---:|
| Visualizar/atualizar | `E72C` |
| Enviar/nuvem | `E898` |
| Unidade/clínica | `E80F` |
| Médico | `E716` |
| Paciente | `E77B` |
| Exame/documento | `E8A5` |
| Histórico/relatório | `E81C` |
| Editar | `E70F` |
| Remover | `E74D` |
| Fechar | `E711` |
| Imprimir | `E749` |
| Informações | `E946` |
| Adicionar | `E710` |
| Comparar | `E8D4` |
| Captura | `E722` |
| Dispositivos | `E772` |
| Tela cheia | `E740` |
| Trocar atendimento | `E8AB` |
| Temporizador | `E823` |

---

## 12. Barra de comandos

- Usar `ToolStrip` para comandos do módulo.
- Fundo `Surface`.
- Altura aproximada de 60 px.
- Ícones 32 × 32 px.
- Texto curto e direto.
- Padding horizontal de 16 px e vertical de 10 px.
- Separadores discretos entre grupos.
- Ações destrutivas no final.
- Usar submenu quando a ação possuir configurações secundárias.

```csharp
toolStrip.GripStyle = ToolStripGripStyle.Hidden;
toolStrip.BackColor = Surface;
toolStrip.ForeColor = TextPrimary;
toolStrip.AutoSize = false;
toolStrip.Height = 60;
toolStrip.Padding = new Padding(16, 10, 16, 10);
toolStrip.ImageScalingSize = new Size(32, 32);
```

### Sequência aprovada para o Uploader

1. Captura Vídeo
2. Tela cheia
3. Enviar para Nuvem
4. Gestão de Envio
5. Trocar atendimento
6. Gerenciar Dispositivo
   - Configurar Dispositivo como submenu

---

## 13. DataGridView

### Aparência escura aprovada

```csharp
void AplicarEstiloGrid(DataGridView grid)
{
    grid.BackgroundColor = ColorTranslator.FromHtml("#2B2B2B");
    grid.BorderStyle = BorderStyle.None;
    grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
    grid.GridColor = ColorTranslator.FromHtml("#3A3A3A");
    grid.EnableHeadersVisualStyles = false;
    grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
    grid.ColumnHeadersHeight = 42;
    grid.RowTemplate.Height = 44;
    grid.AllowUserToAddRows = false;
    grid.AllowUserToDeleteRows = false;
    grid.AllowUserToResizeRows = false;
    grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

    grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
    {
        BackColor = ColorTranslator.FromHtml("#252525"),
        ForeColor = ColorTranslator.FromHtml("#F5F5F5"),
        Font = new Font("Segoe UI Semibold", 10F),
        Padding = new Padding(8, 0, 8, 0),
        Alignment = DataGridViewContentAlignment.MiddleLeft
    };

    grid.DefaultCellStyle = new DataGridViewCellStyle
    {
        BackColor = ColorTranslator.FromHtml("#2B2B2B"),
        ForeColor = ColorTranslator.FromHtml("#F5F5F5"),
        SelectionBackColor = ColorTranslator.FromHtml("#153F59"),
        SelectionForeColor = Color.White,
        Font = new Font("Segoe UI", 9.5F),
        Padding = new Padding(8, 0, 8, 0)
    };

    grid.AlternatingRowsDefaultCellStyle.BackColor =
        ColorTranslator.FromHtml("#252525");
}
```

### Regras da grade

- Cabeçalho deve parecer parte do tema, nunca uma faixa branca padrão.
- Códigos e números podem ficar centralizados ou à direita.
- Nomes e descrições ficam à esquerda.
- Textos longos usam tooltip.
- Seleção usa azul escuro discreto.
- Ações gerais ficam fora da grade.
- Estado vazio deve aparecer centralizado dentro do cartão.
- Remover a coluna branca residual criada automaticamente pelo grid.
- Usar preenchimento proporcional (`Fill`) nas colunas principais.

### Estado vazio

Exemplos:

> Nenhum exame disponível.

> Selecione um exame para visualizar os arquivos.

O texto usa `TextMuted` e deve ficar centralizado na área de conteúdo.

---

## 14. StatusStrip

Usar para informações persistentes e de baixa prioridade:

- quantidade de registros;
- usuário conectado;
- conexão;
- envio em andamento;
- última atualização;
- versão.

Aparência:

- fundo `SurfaceSecondary`;
- borda superior `Divider`;
- texto `TextSecondary`;
- altura entre 32 e 40 px;
- ícones monocromáticos quando necessários.

Não colocar ações primárias no `StatusStrip`.

---

## 15. Janelas fixas responsivas

### Problema

Uma janela fixa desenhada para Full HD pode ser cortada em monitores HD, principalmente quando:

- a resolução é 1366 × 768 ou 1280 × 720;
- a barra de tarefas reduz a área útil;
- o Windows utiliza escala de 125% ou 150%;
- a janela possui `MinimumSize` maior que a área disponível.

### Regra aprovada

| Resolução do monitor | Perfil |
|---|---|
| 1920 × 1080 ou superior | Original, 100% |
| Inferior a 1920 × 1080 | Compacto, 85% |

O sistema detecta automaticamente a resolução do monitor em que a janela será aberta.

### Comportamento obrigatório

1. Preservar o layout atual em Full HD ou superior.
2. Aplicar escala de 85% em HD.
3. Reduzir controles, cartões, margens, paddings e fontes.
4. Não permitir fontes menores que 8.25 pt.
5. Limpar temporariamente `MinimumSize` antes de reduzir.
6. Verificar `Screen.WorkingArea`, não apenas `Screen.Bounds`.
7. Se ainda não couber, aplicar uma segunda redução proporcional.
8. Centralizar a janela dentro da área útil.
9. Não deixar a janela atrás da barra de tarefas.
10. Aplicar somente a janelas realmente fixas.

### Implementação de referência

```csharp
private static void ConfigureAdaptiveFixedWindow(Form form)
{
    EventHandler loadHandler = null;
    loadHandler = delegate
    {
        form.Load -= loadHandler;
        ApplyResolutionProfile(form);
    };
    form.Load += loadHandler;
}

private static void ApplyResolutionProfile(Form form)
{
    Screen screen = form.Owner != null
        ? Screen.FromControl(form.Owner)
        : Screen.FromControl(form);

    Rectangle resolution = screen.Bounds;
    bool compact = resolution.Width < 1920 || resolution.Height < 1080;
    if (!compact) return;

    form.SuspendLayout();
    try
    {
        form.MinimumSize = Size.Empty;
        form.MaximumSize = Size.Empty;
        form.AutoScaleMode = AutoScaleMode.None;

        ScaleFixedWindow(form, 0.85F);

        Rectangle area = screen.WorkingArea;
        int availableWidth = Math.Max(320, area.Width - 32);
        int availableHeight = Math.Max(240, area.Height - 32);

        if (form.Width > availableWidth || form.Height > availableHeight)
        {
            float fitScale = Math.Min(
                (float)availableWidth / form.Width,
                (float)availableHeight / form.Height);
            ScaleFixedWindow(form, fitScale);
        }

        form.MinimumSize = form.Size;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(
            area.Left + Math.Max(0, (area.Width - form.Width) / 2),
            area.Top + Math.Max(0, (area.Height - form.Height) / 2));
    }
    finally
    {
        form.ResumeLayout(true);
    }
}
```

### Redução proporcional com fontes

```csharp
private static void ScaleFixedWindow(Form form, float scale)
{
    if (scale >= 0.999F) return;

    Size clientSize = form.ClientSize;
    Dictionary<Control, Font> fonts = new Dictionary<Control, Font>();
    CaptureFonts(form, fonts);

    form.Scale(new SizeF(scale, scale));
    ApplyScaledFonts(fonts, scale);

    form.ClientSize = new Size(
        Math.Max(320, (int)Math.Round(clientSize.Width * scale)),
        Math.Max(240, (int)Math.Round(clientSize.Height * scale)));
}

private static void CaptureFonts(
    Control parent,
    Dictionary<Control, Font> fonts)
{
    fonts[parent] = parent.Font;
    foreach (Control control in parent.Controls)
        CaptureFonts(control, fonts);
}

private static void ApplyScaledFonts(
    Dictionary<Control, Font> fonts,
    float scale)
{
    foreach (KeyValuePair<Control, Font> entry in fonts)
    {
        Font original = entry.Value;
        float size = Math.Max(8.25F, original.SizeInPoints * scale);
        entry.Key.Font = new Font(
            original.FontFamily,
            size,
            original.Style,
            GraphicsUnit.Point);
    }
}
```

### Cuidados

- Configurar o perfil depois de construir o layout visual.
- Aplicar a redução apenas uma vez.
- Não usar `MinimumSize` Full HD durante a redução.
- Testar rótulos com palavras longas.
- Testar ComboBox e NumericUpDown após a escala.
- Em formulários redimensionáveis, preferir Dock, Anchor e painéis de layout em vez de escalar tudo.

---

## 16. DPI e monitores múltiplos

```csharp
form.AutoScaleMode = AutoScaleMode.Dpi;
form.StartPosition = FormStartPosition.CenterScreen;
```

Em .NET moderno:

```csharp
[STAThread]
static void Main()
{
    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.Run(new FrmPrincipal());
}
```

Em .NET Framework, configurar DPI awareness no manifesto.

Regras:

- Usar `Screen.FromControl(form.Owner)` quando houver janela proprietária.
- Usar `Screen.FromControl(form)` como fallback.
- Centralizar na `WorkingArea` do monitor correto.
- Testar abertura em monitor secundário.

---

## 17. Adaptação de formulários redimensionáveis

Usar:

- `Dock` para blocos principais;
- `Anchor` para ações nas bordas;
- `TableLayoutPanel` para linhas e colunas;
- `FlowLayoutPanel` para botões e indicadores;
- `SplitContainer` para conteúdo e painel lateral;
- `AutoSize` para rótulos controlados;
- `AutoScroll` apenas em áreas de conteúdo.

Regras:

- Abaixo de 1280 px, indicadores podem quebrar em duas linhas.
- Painel lateral deve possuir largura mínima entre 300 e 360 px.
- Ações secundárias podem migrar para submenu.
- Colunas menos importantes podem ser ocultadas em janelas estreitas.
- Evitar cálculos extensos no evento `Resize`.

---

## 18. Padrões por tipo de tela

### Consulta

1. Cabeçalho.
2. Filtros compactos.
3. Título e quantidade de resultados.
4. Barra de ações.
5. `DataGridView` preenchendo o espaço restante.
6. Estado vazio, carregando ou erro dentro do cartão.
7. `StatusStrip`.

### Cadastro

1. Cabeçalho ou título da janela.
2. Campos agrupados por assunto em cartões.
3. Layout com duas colunas quando houver espaço.
4. `Cancelar` e `Salvar` no canto inferior direito.
5. Salvar verde e Cancelar vermelho.
6. Validação próxima aos campos.
7. Perfil compacto automático em HD quando a janela for fixa.

### Vídeo e imagem

1. Conteúdo visual como elemento principal.
2. Proporção previsível, normalmente 16:9.
3. Controles próximos à mídia.
4. Detalhes e configurações no painel lateral.
5. Galeria ou histórico na parte inferior.
6. Estado da câmera, envio e conexão sempre visível.
7. Barra de comandos com ícones 32 × 32 px.

### Login

1. Área lateral institucional escura.
2. Logotipo sem fundo azul ou retângulo residual.
3. Textos da lateral em branco.
4. Campos amplos e alinhados.
5. Botão principal evidente.
6. Nenhum rótulo escondido atrás da marca.

---

## 19. Estados obrigatórios

Toda tela deve prever:

- carregando;
- sem dados;
- com dados;
- erro de carregamento;
- sem permissão;
- operação em andamento;
- operação concluída;
- controles desabilitados;
- janela estreita;
- monitor HD;
- monitor Full HD;
- escala de 125% e 150%.

### Mensagens sugeridas

**Sucesso**

> O exame foi enviado com sucesso.

**Confirmação destrutiva**

> Excluir o arquivo selecionado? Esta ação não poderá ser desfeita.

**Erro recuperável**

> Não foi possível concluir o envio. Verifique a conexão e tente novamente.

**Sem dados**

> Nenhum exame disponível.

O estado vazio deve oferecer uma ação útil quando possível: `Atualizar`, `Adicionar`, `Tentar novamente` ou `Limpar filtros`.

---

## 20. Acessibilidade

- Contraste mínimo de 4,5:1 para texto normal.
- Nunca depender apenas da cor para comunicar estado.
- Revisar `TabIndex`.
- Exibir foco de teclado.
- Usar `AccessibleName` e `AccessibleDescription`.
- Adicionar tooltip a ícones sem texto.
- Área clicável mínima: 32 × 32 px.
- `Enter` executa a ação principal quando seguro.
- `Esc` cancela ou fecha diálogos sem interromper processo crítico sem confirmação.
- Evitar fontes abaixo de 8.25 pt no perfil compacto.

---

## 21. Controles personalizados mínimos

Criar apenas componentes reutilizáveis:

1. `Windows11RoundedPanel` — superfície, borda e raio.
2. `Windows11RoundedFieldHost` — campo nativo com borda arredondada e foco.
3. `ModernButton` ou método central de estilo — variantes primária, positiva e destrutiva.
4. `ModernDataGridView` ou método central de tema.
5. `StatusBadge` — cápsula semântica.
6. `ProgressDataGridViewColumn` — progresso na grade.
7. `DarkToolStripRenderer` — menus e barras consistentes.

Não criar um componente diferente para cada formulário.

---

## 22. Estratégia de implementação

### Classe central por módulo

Cada executável deve possuir uma classe central, por exemplo:

- `MeusExamesVisualTheme`;
- `UploaderVisualTheme`;
- `WebCamVisualTheme`.

O construtor do formulário chama o tema depois de `InitializeComponent()`:

```csharp
public FrmEditarPaciente()
{
    InitializeComponent();
    MeusExamesVisualTheme.Apply(this);
}
```

### Ordem recomendada

1. `InitializeComponent()`.
2. Aplicar cores, fontes e estilos recursivos.
3. Construir cartões e hosts arredondados.
4. Aplicar ícones às barras.
5. Configurar o perfil responsivo das janelas fixas.
6. Carregar dados.

### Restrições

- Não alterar regras de negócio para aplicar o tema.
- Não renomear controles usados pelos eventos.
- Não remover eventos existentes.
- Não alterar outros projetos quando o escopo indicar apenas uma pasta.
- Preferir alteração centralizada a repetir dezenas de propriedades.

---

## 23. Checklist de validação

### Estrutura

- [ ] `AutoScaleMode.Dpi` configurado para o perfil normal.
- [ ] Conteúdo principal com `Dock = Fill` ou layout equivalente.
- [ ] Sem grandes áreas vazias sem propósito.
- [ ] Tela utilizável em 1280 × 720.
- [ ] Janela fixa detecta automaticamente HD e Full HD.
- [ ] Janela não ultrapassa `Screen.WorkingArea`.

### Visual

- [ ] Segoe UI em toda a tela.
- [ ] Fundo geral `#202020`.
- [ ] Cartões `#2B2B2B` com borda `#454545`.
- [ ] Uma ação principal por bloco.
- [ ] Azul usado para ação e seleção.
- [ ] Salvar verde e Cancelar vermelho.
- [ ] Logotipos brancos ou com contraste adequado.

### Ícones e botões

- [ ] Ícones de barras em 32 × 32 px.
- [ ] Ícones monocromáticos brancos e expressivos.
- [ ] Botões retangulares sem ícones.
- [ ] Texto dos botões centralizado.
- [ ] Ações destrutivas no final da barra.

### Controles

- [ ] Campos com borda arredondada e foco visível.
- [ ] Grid completamente escuro, sem colunas ou faixas brancas residuais.
- [ ] Cabeçalho da grade com altura de 42 px.
- [ ] Linhas da grade com altura de 44 px.
- [ ] Estado vazio implementado.
- [ ] Exclusões com confirmação.

### Perfil HD

- [ ] Layout reduzido para 85% abaixo de 1920 × 1080.
- [ ] Fontes também reduzidas.
- [ ] Fonte mínima de 8.25 pt.
- [ ] Rótulos não quebram linha indevidamente.
- [ ] ComboBox e NumericUpDown permanecem utilizáveis.
- [ ] Botões continuam visíveis e alinhados.
- [ ] Janela centralizada na área útil.

### Usabilidade

- [ ] Ordem de tabulação revisada.
- [ ] Enter e Esc avaliados.
- [ ] Mensagens claras.
- [ ] Tooltips em textos truncados e ícones sem texto.
- [ ] Teste em 100%, 125% e 150% de escala.

---

## 24. Prompt completo para gerar ou atualizar telas

```text
Modernize esta tela de um sistema médico em C# Windows Forms seguindo o padrão MEDWARE Windows 11 escuro e responsivo.

Preserve todas as funções, dados, campos, ações, nomes de controles, eventos e fluxo de trabalho. Altere somente o layout e os recursos visuais dentro da pasta indicada.

Use:
- fonte Segoe UI;
- fundo geral #202020;
- superfícies e cartões #2B2B2B;
- superfícies elevadas e campos #323232;
- bordas #454545 de 1 px;
- divisores #3A3A3A;
- texto principal #F5F5F5;
- texto secundário #C7C7C7;
- azul principal #0F6CBD;
- foco #60CDFF;
- verde #107C10 para Salvar e confirmar;
- vermelho #C42B1C para Cancelar, Sair e Fechar;
- cantos suavemente arredondados, entre 6 e 8 px;
- margens externas entre 32 e 40 px em Full HD;
- campos entre 32 e 38 px de altura;
- botões com 40 px de altura, sem ícones e com texto centralizado;
- ícones monocromáticos brancos de 32 × 32 px somente nas barras de comandos;
- ícones Segoe Fluent Icons com fallback para Segoe MDL2 Assets;
- DataGridView completamente escuro, com cabeçalho de 42 px, linhas de 44 px e seleção #153F59;
- estados vazios centralizados e discretos.

Para janelas fixas:
- manter 100% do tamanho em 1920 × 1080 ou superior;
- detectar automaticamente resolução inferior a Full HD;
- aplicar perfil compacto de 85%;
- reduzir proporcionalmente controles, cartões, fontes, margens e paddings;
- não usar fontes menores que 8.25 pt;
- verificar Screen.WorkingArea;
- reduzir mais se ainda não couber;
- centralizar a janela no monitor correto;
- impedir que a barra de tarefas corte a janela.

Use controles WinForms reais e poucos componentes personalizados reutilizáveis: Form, Label, Panel, TableLayoutPanel, FlowLayoutPanel, ToolStrip, SplitContainer, TextBox, ComboBox, NumericUpDown, DateTimePicker, CheckBox, Button, DataGridView, TabControl, StatusStrip, RoundedPanel e RoundedFieldHost.

Não usar:
- padrões exclusivos de aplicações web;
- glassmorphism;
- controles com tamanho de aplicativo móvel;
- grandes áreas vazias;
- faixas saturadas sem função;
- fundo azul atrás do logotipo;
- ícones coloridos inconsistentes nas barras;
- ícones dentro dos botões Salvar e Cancelar;
- textos escondidos, sobrepostos ou quebrados indevidamente;
- tamanho fixo que ultrapasse a área útil do monitor.

Antes de concluir:
1. validar no Designer;
2. validar em execução;
3. testar em 1366 × 768, 1920 × 1080 e escala de 125%;
4. verificar se nenhum controle está sobre outro;
5. verificar se todos os comandos continuam funcionando.
```

---

## 25. Prompt curto

```text
Use esta captura como referência funcional e aplique o padrão MEDWARE Windows 11 escuro e responsivo. Preserve controles, eventos e regras de negócio. Use fundo #202020, cartões #2B2B2B, campos #323232, Segoe UI, azul #0F6CBD, texto branco, bordas #454545, cantos de 6–8 px, botões sem ícones e centralizados, Salvar verde, Cancelar vermelho, ícones Fluent brancos 32 × 32 nas barras e DataGridView escuro. Em janelas fixas, mantenha 100% em Full HD ou superior e aplique automaticamente 85% abaixo de 1920 × 1080, reduzindo também as fontes e garantindo que a janela caiba na Screen.WorkingArea.
```

---

## 26. Critério final de consistência

Uma tela está pronta quando:

- parece pertencer ao mesmo sistema que Meus Exames e Uploader;
- usa integralmente o tema escuro aprovado;
- título, ação principal, filtros e conteúdo são identificados rapidamente;
- pode ser reproduzida com controles reais do Windows Forms;
- estados e ações usam cores consistentes;
- não depende de efeitos gráficos complexos;
- aproveita a área disponível sem ficar apertada ou vazia;
- ícones, botões, campos e tabelas seguem o mesmo padrão;
- funciona em monitor HD sem cortes;
- mantém o tamanho e o layout aprovado em Full HD ou superior;
- componentes repetidos mantêm medidas, fontes e comportamento.

Este guia deve ser tratado como a fonte única de verdade para o redesenho das próximas telas MEDWARE.
