# Guia para agentes — Modelos de ecocardiograma em modo texto (Laudos UX)

Leia este arquivo **antes** de gerar ou editar modelos de ecocardiograma em **modo texto**. Ele consolida as regras reais do botão **Importar** (`btnImportarScript`) na rota `/Script/Editar`, implementadas em `Script.js` → `importarScript()` e `criarEstruturaScript()`.

O agente pode entregar o modelo em **dois formatos equivalentes**:

| Formato | Extensão | Uso |
|---------|----------|-----|
| **TXT (DSL)** | `.txt` | Importado e **convertido automaticamente** para JSON na tela de edição |
| **JSON Studio** | `.json` | Importado **diretamente**; é o formato dos modelos com sufixo `Studio` |

---

## 1. Contexto

| Item | Descrição |
|------|-----------|
| Sistema | Laudos UX — tela `/Script/Editar` |
| Botão | `#btnImportarScript` em `Views/Prontuario/Script.cshtml` → `onclick="importarScript()"` |
| Tipo de script | `tipoScript = 3` (modo texto) |
| Convenção de nome | Modelos interpretáveis via JSON usam sufixo **`Studio`** no título do arquivo |
| Idioma | Respostas ao usuário em **português** |
| Codificação | UTF-8 (fallback ISO-8859-1 se houver ``) |

### Arquivos de referência neste pacote

| Arquivo | Papel |
|---------|-------|
| `ADULTO/1.7 - Ecodopplercardiograma 2024 Grau Studio/MODO TXT/1.7_-_Ecodopplercardiograma_2024_Grau_Studio.txt` | Exemplo canônico do **formato TXT** |
| `ADULTO/1.7 - Ecodopplercardiograma 2024 Grau Studio/SCRIPT/1.7 - Ecodopplercardiograma 2024 Grau Studio.json` | Exemplo canônico do **formato JSON Studio** |
| Demais `*/SCRIPT/* Studio.json` | Variações validadas (Ágil, Pulmonar, 2023, EscalaCor, etc.) |

---

## 2. Como funciona a importação (`btnImportarScript`)

### 2.1 Fluxo completo (`importarScript`)

1. Se o script já existe (`codScriptLaudo ≠ 0`):
   - Se `isEmUso`: pergunta se deseja continuar, alertando que laudos antigos podem deixar de carregar se variáveis forem alteradas ou removidas.
   - Caso contrário: pergunta *"Deseja sobrepor o script atual?"*.
   - Se o usuário cancelar, a importação é abortada.
2. Obtém `codCliente` via `GET /UsuarioApi/LerRegistro` (usuários com `codUsuarioLogado ≠ 1`). O parâmetro é passado a `criarEstruturaScript`, mas **não altera o parse** atualmente.
3. Abre seletor de arquivo aceitando: **`.json`**, **`.txt`**, **`.uxdll`**.
4. Lê o arquivo como `ArrayBuffer` e decodifica em UTF-8; se detectar ``, tenta ISO-8859-1.
5. Define o **título** do modelo a partir do nome do arquivo (remove `.json` ou `.txt`).
6. **Se for `.txt`**: só converte quando `name.includes(".txt") && type === "text/plain"`. Chama `criarEstruturaScript(texto)` → retorna string JSON.
7. **Se for `.json`**: usa o conteúdo diretamente, sem conversão.
8. **Se for `.uxdll`**: aparece no seletor, mas **não há parser dedicado** — o conteúdo segue para `JSON.parse`. Só funciona se o arquivo for JSON válido por dentro.
9. Faz `JSON.parse` do resultado.
10. Chama `definirCamposComGraficosNormalidade(camposScript)`: modal **"Definir gráficos"** (SweetAlert) listando campos numéricos; o usuário marca quais terão `desenho = "-1"`.
11. Carrega na tela com `carregarScript()`, que atribui `etiquetaPai` automaticamente por coluna.
12. Ativa botões de resetar/salvar e registra listeners de drag-and-drop nas colunas.
13. Em caso de erro de parse: exibe *"Erro ao carregar arquivo json."*.

### 2.2 Pré-processamento do TXT (`processarLinhas`)

- Divide por quebras de linha (`\n`).
- Remove espaços nas extremidades (`trim`).
- **Ignora** linhas vazias.
- **Ignora** linhas que contenham `HASH` (metadado/criptografia legado).

### 2.3 Validação de schema (desativada na importação)

A função `validarEstruturaLaudo()` em `util_script.js` existe, mas a chamada em `importarScript` está **comentada**. Mesmo assim, seguir suas regras evita TXT malformado:

- Normalidades devem estar entre parênteses após a unidade.
- Fórmulas em `Código:` devem usar placeholders `<<VR_...>>`.

### 2.4 Pós-importação automática

| Efeito | Quando |
|--------|--------|
| `etiquetaPai` | Atribuído em `carregarScript()` → `agruparPorEtiqueta()` separadamente para colunas 1 e 2 |
| `imprimir = true` | Se ausente no JSON |
| `geraGrafico = true` | Se ausente no JSON |
| Campo `LAUDODESCRITIVO` | Inserido automaticamente ao final de todo TXT importado |

### 2.5 Importação em lote (biblioteca)

Na tela de biblioteca (`manut_script.importarMultiplosScripts`), apenas arquivos **`.json`** são aceitos para cadastro direto via API `POST /Script/CadastrarScript`. Nomes duplicados são ignorados.

### 2.6 Exportação (`exportarScript`)

Inverso parcial da importação:

- Usuário comum (`codUsuarioLogado > 1`): exporta sempre **TXT** via `converterJsonParaTxt()`.
- Usuário admin: escolhe **TXT** ou **JSON**.
- Modelos com campo `html` só podem ser exportados em JSON (aviso na tela).

---

## 3. Escolha do formato de saída

### Quando gerar **TXT**

- O usuário pediu arquivo `.txt` para importação manual.
- O modelo será refinado depois no editor visual.
- Prioridade em legibilidade humana e diff simples.

### Quando gerar **JSON Studio**

- O usuário pediu modelo `Studio` pronto para uso.
- É necessário preservar `etiquetaPai`, `id`, `ordem`, `desenho`, estrutura de duas colunas já calibrada.
- Importação em lote na biblioteca.

### Equivalência

O sistema possui conversão **JSON → TXT** em `converterJsonParaTxt()` (`util_script.js`). Os dois formatos representam o **mesmo modelo**; prefira JSON quando precisar de fidelidade total à estrutura Studio.

---

## 4. O que o importador TXT interpreta

### 4.1 Mapa de tipos de linha

| Sintaxe TXT | Tipo gerado (`tipo`) | Observação |
|-------------|----------------------|------------|
| `[NOME DA SEÇÃO]` | `etiqueta` | Linha começa com `[` |
| `*Descrição (VAR)` | `label` | Linha começa com `*` |
| `Descrição (VAR): valor unidade ...` | `numero` | Contém `:` após o nome |
| `Descrição (VAR) = op1, op2, op3 [Lista]` | `combo` | Marcador `[Lista]` |
| `Descrição (VAR) = op1, op2 [Única]` ou `[UNICA]` | `radio` | Marcador de seleção única |
| `Descrição (VAR) = op1, op2 [Múltipla]` ou `[MULTIPLA]` | `check` | Marcador de seleção múltipla |
| *(automático ao final)* | `textoLongo` | Campo `LAUDODESCRITIVO` |

### 4.2 Tipos **não** gerados pelo importador TXT

Estes tipos existem no editor (`eTipoCampo`), mas **não** são criados pelo parser TXT:

`accordion`, `botao`, `textArea`, `textoCurto`, `html`

Para usá-los, importe **JSON** diretamente ou adicione manualmente no editor.

### 4.3 Palavras-chave reconhecidas (case-insensitive)

O parser normaliza acentos via `corrigirTextoComAcento()`:

| Escrita aceita | Uso |
|----------------|-----|
| `Comentário` / `Comentario` | Faixas coloridas de classificação |
| `Código` / `Codigo` | Fórmula JavaScript calculada |
| `[Lista]` / `[lista]` | Campo combo |
| `[Única]` / `[UNICA]` | Campo radio |
| `[Múltipla]` / `[MULTIPLA]` | Campo check |

---

## 5. Formato TXT (DSL de importação)

### 5.1 Estrutura geral

```text
[SEÇÃO EM MAIÚSCULAS]
Descrição do campo (NOME_VAR): valor_padrao  unidade (M: min a max) (F: min a max)
Descrição (VAR) = Opção A, Opção B, Opção C [Lista]
...
```

- **Uma linha = um campo** (ou uma etiqueta de seção).
- Linha em branco entre seções é opcional, mas recomendada.
- **Nunca** concatenar dois campos na mesma linha após `[Lista]` (erro comum a evitar).

### 5.2 Etiquetas de seção

```text
[DADOS GERAIS]
[AORTA]
[ÁTRIO ESQUERDO]
```

Regras do parser:

- Linha começa com `[` → tipo `etiqueta`.
- Texto interno vira `descricao`; `nome` = texto em maiúsculas, espaços → `_`, sem acentos nem caracteres especiais.
- Se `descricao` repetir, o sistema acrescenta sufixo `_1`, `_2`… no `nome`.
- `ocultarDescricao` importado como `false` (diferente dos JSON Studio de produção, que costumam usar `true`).

### 5.3 Label (texto fixo)

```text
*Observação clínica (OBS_CLINICA)
```

- Linha começa com `*`.
- Extrai variável do último `(NOME)` na linha, se existir.
- Tipo gerado: `label`.

### 5.4 Campo numérico

**Padrão mínimo:**

```text
Altura (ALTURA): 0.0  cm (F:  a ) (M:  a )
```

**Com referência de normalidade por sexo:**

```text
Anel (AO): 0.0  mm (M: 19 a 23.4) (F: 17.4 a 21.6)
```

**Com faixas de cor (Comentário):**

```text
Anel (AO): 0.0  mm (M: 19 a 23.4) (F: 17.4 a 21.6)  Comentário: (F: {0, 17,3,  Azul }),(F: {17,4, 21,6,  verde }),(F: {21,7, 23,  amarelo }),(F: {23,1, 25,  laranja }),(F: {25,1, 999,  vermelho }),(M: {0, 18,  azul }),(M: {19, 23,4,  verde }),(M: {23,5, 25,  amarelo }),(M: {25,1, 27,  laranja }),(M: {27,1, 999,  vermelho })
```

**Com fórmula calculada:**

```text
IMC (IMC): 0.0  kg/m² (F:  a ) (M:  a )  Código: if(<<VR_PESO>> > 0 && <<VR_ALTURA>> > 0) { (<<VR_PESO>> / Math.pow(<<VR_ALTURA>> / 100, 2)).toFixed(1) } else { '' }
```

**Com Comentário e Código na mesma linha:**

```text
Sup. Corp (SUPCOR): 0.0  m² (F:  a ) (M:  a )  Código: if(<<VR_PESO>> > 0 && <<VR_ALTURA>> > 0) { (0.007184 * Math.pow(<<VR_PESO>>, 0.425) * Math.pow(<<VR_ALTURA>>, 0.725)).toFixed(2) } else { '' }
```

#### Anatomia de um campo numérico

```text
{Descrição} ({NOME_VAR}): {valor_padrao}  {unidade} ({referências}) [{Comentário: ...}] [{Código: ...}]
```

| Parte | Regra |
|-------|-------|
| `Descrição` | Texto legível na tela |
| `NOME_VAR` | Identificador em parênteses; vira `nome` do campo (sem `VR_` no TXT) |
| `valor_padrao` | Geralmente `0.0`; define `casasDecimais` automaticamente |
| `unidade` | Segundo token após o valor (ex.: `mm`, `ml`, `m/s`, `%`, `mmHg`, `kg/m²`) |
| `(M: X a Y)` / `(F: X a Y)` | Referência de normalidade por sexo; separador **` a `** (com espaços) |
| `Comentário:` | Faixas coloridas no formato `(Sexo: {min, max, cor})` ou `(Sexo: {min, max, rótulo, cor})` |
| `Código:` | Expressão JavaScript; variáveis como `<<VR_NOME>>` |

#### Referências de normalidade interpretadas

| Formato no TXT | Efeito em `referenciaNormalidade` |
|----------------|-------------------------------------|
| `(M: 19 a 23.4)` | `valorMin=19`, `valorMax=23.4`, `valorExtenso="19 a 23.4"` |
| `(F:  a )` / vazio | Referência criada com min/max vazios ou inválidos |
| `(>= 0,15)` ou `(≥ 0,15)` | `valorMin=0.15`, `valorMax=999` |
| `(<= 34)` ou `(≤ 34)` | `valorMin=-999`, `valorMax=34` |
| `(> 17 mm)` | `valorMin=17`, `valorMax=999` |
| `(< 34 ml/m²)` | `valorMin=-999`, `valorMax=34` |
| `(200 ± 40 ms)` | min/max calculados a partir dos dois números |
| `(0.8 + \| - 0.2)` ou `(0.8+|-0.2)` | faixa simétrica em torno do valor central |
| Referência sem `M:`/`F:` com `< valor` | Aplicada a ambos os sexos |

**Cores aceitas no Comentário:** `azul`, `verde`, `amarelo`, `laranja`, `vermelho` (minúsculas no TXT).

**Separador decimal nas faixas:** vírgula (`17,4`) — o parser converte com operador unário `+`.

**Faixas do Comentário:** cada `(Sexo: { ... })` gera entrada em `normalidades` com `valorMin`, `valorMax`, `cor` e `valorExtenso` (quando há rótulo de 4 partes).

#### Propriedades automáticas de campos numéricos importados

| Propriedade | Valor na importação |
|-------------|---------------------|
| `tipo` | `"numero"` |
| `geraGrafico` | `true` |
| `desenho` | `"-1"` se `referenciaNormalidade` tiver min/max numéricos válidos; senão `"0"` |
| `medida` | segundo token após valor padrão |
| `casasDecimais` | derivado do valor padrão (`0.0` → 1 casa) |
| `valorPadrao` | `""` (valor do TXT não é preservado) |

### 5.5 Campos de lista (combo / radio / check)

```text
Ritmo (RITMO) = Ritmo Sinusal, Taquicardia Sinusal, Bradicardia Sinusal, ... [Lista]
```

| Marcador no final | Tipo gerado |
|-------------------|-------------|
| `[Lista]` | `combo` |
| `[Única]` ou `[UNICA]` | `radio` |
| `[Múltipla]` ou `[MULTIPLA]` | `check` |

Regras:

- Parte antes do `=` → descrição e variável entre parênteses.
- Parte depois do `=` → opções separadas por vírgula.
- O marcador `[Lista]` / `[Única]` / `[Múltipla]` é removido antes do parse das opções.
- `vertical` importado como `"0"`.

Exemplo completo:

```text
Inspiração (INSPIRACAO) = superficial, profunda [Lista]
```

### 5.6 Variáveis em fórmulas (`Código:`)

| Regra | Exemplo |
|-------|---------|
| Placeholder | `<<VR_NOME_VAR>>` |
| Condicional | `if(<<VR_VED>> > 0) { ... } else { '' }` |
| Operadores | `+`, `-`, `*`, `/`, `**`, `Math.pow`, `Math.round`, `.toFixed(n)` |
| Strings em combo | `` `<<VR_INSPIRACAO>>` == "profunda" ? ... `` |
| Retorno vazio | `''` quando não há dados para calcular |

O parser **não** altera o conteúdo de `Código:` além de extrair a substring após `Código: ` (com espaço).

### 5.7 Layout em duas colunas

O importador TXT distribui campos em **coluna 1** e **coluna 2**:

1. Todos os campos começam na **coluna 1**.
2. O sistema calcula o índice do meio do arquivo (`Math.floor(linhas.length / 2) - 1`).
3. Na **primeira etiqueta `[...]` encontrada a partir desse índice**, passa para **coluna 2** e reinicia a numeração de linha (`cont = 0`).

**Implicação para o agente:** organize as seções na ordem desejada nas duas colunas. A divisão visual no editor depende da posição relativa das etiquetas no arquivo. Nos modelos Studio JSON, `coluna: 1` ou `coluna: 2` é explícito.

### 5.8 Campo automático ao final

Todo TXT importado recebe automaticamente:

```json
{
  "coluna": -1,
  "descricao": "Laudo Descritivo",
  "nome": "LAUDODESCRITIVO",
  "tipo": "textoLongo",
  "valorPadrao": "<p><br></p>",
  "imprimir": true,
  "geraGrafico": false,
  "funcao": ""
}
```

Nos JSON Studio de produção, `etiquetaPai` costuma apontar para a última seção relevante (atribuído ao carregar na tela).

---

## 6. Formato JSON Studio

### 6.1 Estrutura raiz

```json
{
  "camposScript": [ ... ]
}
```

Somente a propriedade `camposScript` é obrigatória para importação.

### 6.2 Tipos de campo (`tipo`)

| Valor | Uso em ecocardiograma | Importável via TXT |
|-------|----------------------|-------------------|
| `etiqueta` | Título de seção | Sim |
| `label` | Texto fixo/informativo | Sim (`*`) |
| `numero` | Medidas, índices, percentuais, velocidades | Sim |
| `combo` | Listas em dropdown | Sim (`[Lista]`) |
| `radio` | Seleção única | Sim (`[Única]`) |
| `check` | Seleção múltipla | Sim (`[Múltipla]`) |
| `textoLongo` | Laudo descritivo (Quill) | Automático ao final do TXT |
| `textArea` | Texto multilinha | Somente JSON |
| `textoCurto` | Texto curto | Somente JSON |
| `botao` | Botão de ação | Somente JSON |
| `accordion` | Seção expansível | Somente JSON |
| `html` | HTML embutido | Somente JSON |

### 6.3 Propriedades comuns

| Propriedade | Descrição |
|-------------|-----------|
| `id` | Identificador numérico único sequencial no array |
| `nome` | Código da variável (ex.: `ALTURA`, `AO`, `RITMO`) |
| `descricao` | Rótulo exibido na tela |
| `coluna` | `1` (esquerda), `2` (direita) ou `-1` (laudo descritivo, largura total) |
| `linha` | Ordem vertical dentro da coluna |
| `ordem` | Ordem de tabulação (string ou número) |
| `imprimir` | `true` para incluir na impressão |
| `ocultarDescricao` | `true` em etiquetas de seção (Studio de produção) |
| `geraGrafico` | `true` em campos numéricos com gráfico |
| `etiquetaPai` | `nome` da etiqueta pai; atribuído automaticamente ao carregar se ausente |

### 6.4 Campo numérico (JSON)

```json
{
  "coluna": 1,
  "imprimir": true,
  "ocultarDescricao": false,
  "linha": 1,
  "geraGrafico": true,
  "casasDecimais": "1",
  "descricao": "Onda S' septal",
  "desenho": 0,
  "medida": "m/s",
  "nome": "SLINHA",
  "textoEscondido": false,
  "ordem": "41",
  "tipo": "numero",
  "valorPadrao": "",
  "funcao": "",
  "normalidades": [
    {
      "sexo": "F",
      "valorMin": "0",
      "valorMax": "0,06",
      "descricao": "Normal",
      "cor": "verde"
    }
  ],
  "referenciaNormalidade": [
    {
      "sexo": "M",
      "valorMin": "0",
      "valorMax": "0,15",
      "unidadeMedida": "m/s",
      "valorExtenso": "≤ 0,15"
    }
  ],
  "id": 4,
  "valorIndexado": null,
  "etiquetaPai": "VE_FUNCAO_SISTOLICA__LINEAR"
}
```

| Propriedade | Detalhe |
|-------------|---------|
| `casasDecimais` | String (`"0"`, `"1"`, `"2"`) |
| `desenho` | `0` = sem gráfico; `-1` = com gráfico de normalidade |
| `medida` | Unidade do campo (sem repetir nas descrições de normalidade) |
| `funcao` | JavaScript com `<<VR_VAR>>`; pode usar quebras `\n` |
| `normalidades` | Faixas com cor para exibição/comentário |
| `referenciaNormalidade` | Intervalo de referência por sexo |
| `valorIndexado` | Geralmente `null` |

**Cores em `normalidades.cor`:** `verde`, `vermelho`, `amarelo`, `laranja`, `azul`.

### 6.5 Campo combo (JSON)

```json
{
  "coluna": 1,
  "descricao": "Ritmo",
  "nome": "RITMO",
  "tipo": "combo",
  "opcoes": [
    " Ritmo Sinusal",
    " Taquicardia Sinusal",
    "Bradicardia Sinusal"
  ],
  "vertical": "0",
  "ordem": "5",
  "id": 11,
  "etiquetaPai": "DADOS_GERAIS"
}
```

### 6.6 Etiqueta de seção (JSON)

```json
{
  "coluna": 1,
  "imprimir": true,
  "ocultarDescricao": true,
  "linha": 0,
  "geraGrafico": false,
  "descricao": "DADOS GERAIS",
  "nome": "DADOS_GERAIS",
  "tipo": "etiqueta",
  "ordem": 200,
  "id": 1
}
```

### 6.7 Laudo descritivo (JSON)

```json
{
  "coluna": -1,
  "descricao": "Laudo Descritivo",
  "nome": "LAUDODESCRITIVO",
  "tipo": "textoLongo",
  "valorPadrao": "<p><br></p>",
  "linha": 47,
  "ordem": "77",
  "id": 89,
  "etiquetaPai": "VE_FUNCAO_SISTOLICA_FENOTIPOS"
}
```

### 6.8 Organização típica de um ecocardiograma Studio

Ordem usual das seções (coluna 1 / coluna 2 intercaladas conforme modelo de referência):

| Seção | Conteúdo típico |
|-------|-----------------|
| DADOS GERAIS | Altura, peso, superfície corporal, IMC, ritmo |
| AORTA | Anel, seios de Valsalva, JST, ascendente, arco |
| ÁTRIO ESQUERDO | AE, volumes, indexados |
| V.E (LINEAR) | VED, VES, septo, PP, massa, ERP |
| V.E (Volume) | VDF, VSF, indexados (Teichholz) |
| V.E (FUNÇÃO SISTÓLICA) | FE, Simpson, MAPSE, S', GLS |
| FUNÇÃO DIASTÓLICA | E, A, e', E/e', desaceleração |
| CORAÇÃO DIREITO | AD, VD, VCI, TAPSE, FAC |
| PRESSÕES PULMONARES | PSAP, gradientes, regurgitação tricúspide |
| Laudo Descritivo | Texto livre (`textoLongo`, coluna -1) |

Use o JSON `1.7 - Ecodopplercardiograma 2024 Grau Studio.json` como blueprint de seções, nomes de variáveis e fórmulas.

---

## 7. Convenções de nomenclatura

| Regra | Exemplo |
|-------|---------|
| Sufixo do arquivo/título | `... Studio` para modelos modo texto JSON |
| Variáveis | MAIÚSCULAS, underscore (`AO_SV`, `VR_PESO` só dentro de fórmulas) |
| Etiquetas | Texto da seção → `nome` sem acento (`FUNCAO_DIASTOLICA_DO_VE`) |
| Indexados | Sufixo `_I` ou descritivo (`VEDI`, `AO_ANEL_I`) |
| TXT para importação | Pode usar `_` no nome do arquivo (ex.: `1.7_-_Ecodopplercardiograma_2024_Grau_Studio.txt`) |
| Unidades | Manter em `medida` do campo; **não** repetir nas descrições de normalidade |

---

## 8. Fluxo de trabalho do agente

Ao receber pedido de novo modelo ou alteração:

1. **Identificar** se a saída deve ser `.txt`, `.json` ou ambos.
2. **Ler** o modelo Studio de referência mais próximo (mesma versão/ano).
3. **Listar** seções, campos, fórmulas e normalidades necessárias.
4. **Gerar** seguindo as regras das seções 5 (TXT) ou 6 (JSON).
5. **Validar** com o checklist da seção 9.
6. **Informar** ao usuário como importar: `/Script/Editar` → botão Importar → selecionar arquivo.
7. **Responder** em português.

### Ao criar modelo novo a partir de especificação clínica

1. Comece pelas medidas obrigatórias da diretriz/versão (ex.: ASE 2024).
2. Replique a estrutura de seções do modelo irmão mais recente.
3. Adicione `Código:` para todos os campos calculados (índices, FE, massa, PSAP, etc.).
4. Configure `Comentário:` com faixas de cor quando houver classificação (normal / limítrofe / alterado).
5. Feche com campo `textoLongo` para laudo descritivo (automático no TXT).

---

## 9. Checklist de validação

| Verificação | TXT | JSON |
|-------------|-----|------|
| Arquivo parseável como JSON após importação | ✓ | ✓ |
| MIME `text/plain` para `.txt` | ✓ | N/A |
| Uma linha por campo | ✓ | N/A |
| Etiquetas `[SEÇÃO]` / `tipo: etiqueta` | ✓ | ✓ |
| Variáveis únicas | ✓ | ✓ |
| Fórmulas com `<<VR_...>>` | ✓ | ✓ |
| `(M: ...)` e `(F: ...)` coerentes | ✓ | ✓ |
| `Comentário:` com `{min, max, cor}` | ✓ | `normalidades` |
| Listas com `[Lista]` em linha própria | ✓ | `opcoes` array |
| Campo laudo descritivo | automático | `textoLongo`, `coluna: -1` |
| `id` únicos sequenciais | N/A | ✓ |
| `etiquetaPai` em todos os campos não-etiqueta | automático ao carregar | ✓ |
| Nome do arquivo sem caracteres inválidos | ✓ | ✓ |
| Sem linhas `HASH` | ✓ | N/A |
| Unidade apenas em `medida`, não nas faixas | N/A | ✓ |

---

## 10. Exemplos mínimos

### 10.1 TXT — trecho inicial

```text
[DADOS GERAIS]
Altura (ALTURA): 0.0  cm (F:  a ) (M:  a )
Peso (PESO): 0.0  kg (F:  a ) (M:  a )
Sup. Corp (SUPCOR): 0.0  m² (F:  a ) (M:  a )  Código: if(<<VR_PESO>> > 0 && <<VR_ALTURA>> > 0) { (0.007184 * Math.pow(<<VR_PESO>>, 0.425) * Math.pow(<<VR_ALTURA>>, 0.725)).toFixed(2) } else { '' }
IMC (IMC): 0.0  kg/m² (F:  a ) (M:  a )  Código: if(<<VR_PESO>> > 0 && <<VR_ALTURA>> > 0) { (<<VR_PESO>> / Math.pow(<<VR_ALTURA>> / 100, 2)).toFixed(1) } else { '' }
Ritmo (RITMO) = Ritmo Sinusal, Fibrilação Atrial, Flutter Atrial [Lista]

[AORTA]
Anel (AO): 0.0  mm (M: 19 a 23.4) (F: 17.4 a 21.6)  Comentário: (F: {17,4, 21,6,  verde }),(M: {19, 23,4,  verde })
```

### 10.2 JSON Studio — trecho mínimo

```json
{
  "camposScript": [
    {
      "coluna": 1,
      "imprimir": true,
      "ocultarDescricao": true,
      "linha": 0,
      "geraGrafico": false,
      "descricao": "DADOS GERAIS",
      "nome": "DADOS_GERAIS",
      "tipo": "etiqueta",
      "ordem": 200,
      "id": 1
    },
    {
      "coluna": 1,
      "imprimir": true,
      "ocultarDescricao": false,
      "linha": 1,
      "geraGrafico": true,
      "casasDecimais": "0",
      "descricao": "Altura",
      "desenho": 0,
      "medida": "cm",
      "nome": "ALTURA",
      "ordem": "1",
      "tipo": "numero",
      "valorPadrao": "",
      "funcao": "",
      "normalidades": [],
      "referenciaNormalidade": [
        { "sexo": "F", "valorMin": "", "valorMax": "", "unidadeMedida": "cm", "valorExtenso": "" },
        { "sexo": "M", "valorMin": "", "valorMax": "", "unidadeMedida": "cm", "valorExtenso": "" }
      ],
      "id": 2,
      "valorIndexado": null,
      "etiquetaPai": "DADOS_GERAIS"
    },
    {
      "coluna": -1,
      "descricao": "Laudo Descritivo",
      "nome": "LAUDODESCRITIVO",
      "tipo": "textoLongo",
      "valorPadrao": "<p><br></p>",
      "linha": 2,
      "ordem": "99",
      "id": 3,
      "imprimir": true,
      "geraGrafico": false,
      "ocultarDescricao": false,
      "funcao": "",
      "textoEscondido": false
    }
  ]
}
```

---

## 11. Anti-padrões (não fazer)

| Anti-padrão | Motivo |
|-------------|--------|
| Dois campos na mesma linha após `[Lista]` | Parser não separa corretamente |
| `Código:` sem `<<VR_...>>` | Não referencia variáveis do script |
| Usar `VR_` no nome entre parênteses do TXT | O sistema já prefixa internamente nas fórmulas |
| JSON sem `camposScript` | Importação falha no `JSON.parse` |
| `id` duplicados no JSON | Conflitos no editor |
| Omitir unidade após valor padrão | `casasDecimais` e `medida` incorretos |
| Copiar fórmulas sem validar dependências | Campos calculados retornam vazio |
| Alterar `tipoScript` | Modo texto exige valor `3` |
| TXT salvo sem MIME `text/plain` | Importador ignora conversão e tenta `JSON.parse` |
| Confiar em `.uxdll` para TXT | Não há parser; precisa ser JSON internamente |
| Repetir unidade nas descrições de normalidade | Unidade pertence ao campo (`medida`) |

---

## 12. O que NÃO alterar sem pedido explícito

- Fórmulas validadas em modelos Studio de produção.
- Nomes de variáveis (`nome`) já usados em laudos existentes no cliente.
- Estrutura de seções de modelos publicados (pode quebrar laudos antigos).
- Criar commit git sem o usuário solicitar.

---

## 13. Referência técnica (código-fonte LaudosUX)

| Arquivo | Função |
|---------|--------|
| `LaudosUX/ClinicasWebMVC/Views/Prontuario/Script.cshtml` | Botão `#btnImportarScript` |
| `LaudosUX/ClinicasWebMVC/wwwroot/js/mdw/Script.js` | `importarScript()`, `criarEstruturaScript()`, `processarLinhas()`, `encontrarProximoItem()`, `definirCamposComGraficosNormalidade()`, `carregarScript()` |
| `LaudosUX/ClinicasWebMVC/wwwroot/js/mdw/util_script.js` | `validarEstruturaLaudo()`, `converterJsonParaTxt()` |
| `LaudosUX/ClinicasWebMVC/wwwroot/js/mdw/manut_script.js` | `importarMultiplosScripts()` (somente JSON) |

### Funções internas do parser TXT (`criarEstruturaScript`)

| Função | Papel |
|--------|-------|
| `corrigirTextoComAcento()` | Normaliza `Comentário`, `Código`, `Única`, `Múltipla` |
| `returnArrComent()` | Extrai trechos entre parênteses |
| `extrairFaixasPorSexo()` | Parse de `{min, max, cor}` ou `{min, max, rótulo, cor}` |
| `verificarValoresMinMax()` / `validarMinMax()` | Define se `desenho = "-1"` |
| `contarCasasDecimais()` | Deriva `casasDecimais` do valor padrão |
| `formatarNome()` | Fallback quando `(VAR)` ausente |
| `ehEtiqueta()` | Detecta linhas que começam com `[` |

---

*Documento para agentes de IA que criam modelos de ecocardiograma em modo texto no Laudos UX. Atualizado com base no código em `Script.js` e `util_script.js` (LaudosUX).*
