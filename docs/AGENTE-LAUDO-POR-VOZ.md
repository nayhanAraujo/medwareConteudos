# Guia para agentes — Laudo por voz (Studio)

Leia este arquivo junto com [`AGENTE-MODELOS-MODO-TEXTO.md`](AGENTE-MODELOS-MODO-TEXTO.md) e [`manual_scripts_html_UX.md`](manual_scripts_html_UX.md).

## Objetivo

Interpretar **fala em português** do médico e manter/atualizar um modelo LaudosUX em **JSON Studio** (`camposScript`), depois exportar para **TXT modo texto** ou **HTML**.

## Estado da sessão

- Formato canônico: `{ "camposScript": [ ... ] }`
- Tipos: `etiqueta`, `label`, `numero`, `combo`, `radio`, `check`, `textoLongo`
- **Não** incluir `LAUDODESCRITIVO` (importador adiciona)

## Intents da API (Build vs Edit)

| Intent | Quando usar | Comportamento |
|--------|-------------|---------------|
| **build** | Sessão do zero; médico descreve o laudo completo | Agente gera `camposScript` inteiro a partir do transcript |
| **edit** | Modelo já existe; comandos pontuais | Agente altera JSON existente (add/remove/move) |

### Build — sessão do zero (sem imagem)

1. Médico fala descrição natural em PT-BR (STT no browser ou Whisper).
2. Transcript é enviado com `intent=build`.
3. Agente interpreta seções, campos, unidades, normalidades M/F ou únicas.
4. Corrigir erros típicos de STT (ex.: "via seda" → via de saída do VE).
5. Retornar JSON Studio completo — **não** exigir comandos "adicionar" ou "remover".

Exemplo de fala:
> "Quero um modelo com dados do paciente: altura, peso, superfície corporal. Outra seção câmaras esquerda: via de saída do VE em mm, anel aórtico em mm, seios de Valsalva com normalidade 28.5 a 35.9."

## Intents suportados (edit)

| Intent | Exemplos de fala |
|--------|------------------|
| addField | "Adiciona anel aórtico em milímetros, homem 19 a 23, mulher 17 a 21" |
| removeField | "Remove o campo peso" |
| moveField | "Move altura para a seção dados gerais" |
| renameSection | "Renomeia seção câmaras para câmaras esquerdas" |
| addSection | "Cria seção valvas" |
| setUnit | "Altera unidade do anel para mm" |
| setNormalidade | "Normalidade masculina 19 a 26" |
| addFormula | "IMC calculado com peso e altura" |
| setListOptions | "Ritmo: ritmo sinusal, fibrilação atrial, flutter [Lista]" |

## Mapeamento fala → JSON

### Campo numérico

```json
{
  "tipo": "numero",
  "descricao": "Anel aórtico",
  "nome": "AO",
  "medida": "mm",
  "coluna": 1,
  "referenciaNormalidade": [
    { "sexo": "M", "valorMin": "19", "valorMax": "23", "unidadeMedida": "mm" },
    { "sexo": "F", "valorMin": "17", "valorMax": "21", "unidadeMedida": "mm" }
  ]
}
```

### Etiqueta de seção

```json
{
  "tipo": "etiqueta",
  "descricao": "DADOS GERAIS",
  "nome": "DADOS_GERAIS",
  "coluna": 1,
  "ordem": 200
}
```

## Bootstrap por imagem

Quando houver imagem inicial:
1. Extrair campos visíveis com seções e normalidades
2. Retornar JSON Studio completo
3. Utterances seguintes **editam** esse modelo (remover, mover, adicionar)

## Exportação

- **modoTexto**: uma linha por campo, `[SEÇÃO]`, regras DSL seção 10.1 de AGENTE-MODELOS-MODO-TEXTO.md
- **html**: fragmento LaudosUX com `containerHtml`, campos `VR_*`, `iniciarFuncoes()`

## Resposta do agente (apply)

JSON estrito:

```json
{
  "camposScript": [ ... ],
  "summary": "Adicionado campo Anel aórtico (AO) na seção AORTA.",
  "warnings": []
}
```

## Anti-padrões

- Não duplicar variáveis `(NOME)` / `nome` no JSON
- Não usar prefixo `VR_` no `nome` do TXT (só em HTML e fórmulas `<<VR_...>>`)
- Não concatenar dois campos na mesma linha TXT
- Preservar ids únicos incrementais
