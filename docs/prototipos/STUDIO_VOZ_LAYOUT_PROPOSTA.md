# Proposta de layout — `/studio/voz`

Análise da rota atual e recomendações de UX para o usuário final (médico / editor de modelos), com protótipo em [`studio-voz-layout.html`](./studio-voz-layout.html).

## Como a tela funciona hoje

A página `frontend/nuxt-app/pages/studio/voz.vue` concentra **três apresentações** no mesmo URL:

| Modo | Quando aparece | Componentes principais |
|------|----------------|------------------------|
| Edição guiada (temporária) | `guidedVoice` ligado e sem rascunhos persistentes | `TemporaryVoice` |
| Editor integrado | `guidedVoice` + `persistentDrafts`, ou checkbox “Usar editor integrado…” | `IntegratedEditor` |
| Fluxo convencional | fallback / “Outras opções” | `VoiceSessionSetup` + `VoiceRecorder` + preview + gerar |

Há ainda variação **conversação contínua (xAI)** vs **gravação por etapas**, importação JSON, bootstrap por imagem e painéis de histórico/validação.

A edição guiada (`TemporaryVoice`) já aponta na direção certa: modelo à esquerda, assistente de voz à direita, escolha clara de origem. O fluxo convencional e a coexistência de modos ainda geram atrito.

## Problemas de usabilidade

1. **Vários “produtos” na mesma rota**  
   Checkbox de editor integrado, details “Outras opções”, voltar à edição guiada e checkbox de tempo real competem com a tarefa principal (“montar o laudo falando”).

2. **Jargão técnico na superfície**  
   ID de sessão, “JSON Studio”, “modoTexto”, “Web Speech API”, “agente xAI”, “camposScript”. Isso é útil para suporte, mas atrapalha o usuário final.

3. **Ação principal pouco destacada**  
   No convencional, “Falar”, “Gerar modelo e TXT”, “Editar modelo com voz” e “Gerar laudo” têm pesos visuais parecidos. O usuário não sabe se deve parar o microfone, clicar em gerar ou editar.

4. **Preview e microfone no mesmo nível**  
   Grid 50/50 coloca a fala e o resultado lado a lado sem hierarquia. O modelo em construção deveria ser o centro; a voz, o controle lateral.

5. **Jornada sem etapas visíveis**  
   Começar → falar → revisar → baixar existe no fluxo, mas não na interface. O usuário não vê “em que passo estou”.

6. **Estados misturados**  
   Trabalho temporário, rascunho persistente, modelo importado e sessão convencional usam avisos diferentes e pouco padronizados (âmbar, toasts, subtítulos longos).

7. **Mobile**  
   A guiada já prioriza o assistente no topo; o convencional não. Em celular, o microfone precisa permanecer óbvio sem perder o modelo.

## Princípios da proposta

1. **Uma jornada padrão** — “Começar → Editar por voz → Revisar → Baixar”.
2. **Uma ação primária** — botão grande de microfone (Falar / Parar).
3. **Modelo no centro** — preview das colunas como área de trabalho.
4. **Assistente sticky** — painel direito (desktop) / faixa superior (mobile) só com voz, frase e aplicar.
5. **Linguagem do usuário** — “Seu modelo”, “Alteração aplicada”, “Baixar modelo”; detalhes técnicos em “Avançado”.
6. **Progressive disclosure** — importar JSON, imagem, conversação contínua e fluxo legado ficam em opções secundárias.
7. **Status legível** — badge de estado (Microfone pausado / Ouvindo / Aplicando / Pronto para baixar).

## Layout sugerido

### Tela 1 — Como deseja começar?

Quatro cards grandes (já existentes na guiada), com copy mais curta:

- **Novo modelo** — “Comece vazio e monte falando”
- **Abrir modelo** — “Use um modelo já cadastrado”
- **Importar arquivo** — “Traga um JSON existente” (em vez de “Importar JSON” sozinho)
- **Usar imagem** — “Partir de um layout escaneado”

CTA único por opção (“Começar”, “Abrir”, “Importar”, “Continuar”). Trabalhos anteriores (se houver rascunhos) aparecem em bloco separado **Continuar de onde parou**.

### Tela 2 — Workspace de edição

```
┌──────────────────────────────────────────────────────────────┐
│ Eco Doppler · Trabalho temporário          [Baixar modelo]   │
│ Etapas: ● Começar  ● Editar por voz  ○ Revisar  ○ Baixar     │
├────────────────────────────┬─────────────────────────────────┤
│ Seu modelo                 │ Assistente de voz               │
│ [Coluna 1] [Coluna 2]      │ ● Microfone pausado             │
│ seções + medidas           │ [ 🎤 Falar alteração ]          │
│ remoção com confirmação    │ Frase reconhecida…              │
│                            │ [ Aplicar ]                     │
│                            │ Últimas alterações (resumo)     │
│                            │ ▸ Opções avançadas              │
└────────────────────────────┴─────────────────────────────────┘
```

- Remover da superfície: ID de sessão, checkbox de tempo real, botões duplicados de build/edit.
- “Parar gravação” aplica só se a frase for confirmada (padrão da guiada: falar → revisar → aplicar), evitando surpresas.
- Download permanece no cabeçalho; gerar TXT/HTML fica em “Baixar modelo” com escolha de formato no clique (ou menu curto).

### O que esconder em “Avançado”

- Conversação contínua vs gravação por etapas  
- Fluxo convencional legado  
- Editor integrado / versões (quando for recurso de power user)  
- Transcript bruto completo e JSON preservado  

## Prioridade de implementação sugerida

| Prioridade | Mudança | Impacto |
|------------|---------|---------|
| Alta | Adotar a guiada como UX padrão quando `guidedVoice` estiver ligado; não expor convencional no primeiro viewport | Clareza imediata |
| Alta | Stepper + status único no cabeçalho do workspace | Orientação |
| Alta | Um CTA de microfone + “Aplicar alteração”; unificar labels | Menos erro |
| Média | Renomear textos técnicos na UI do usuário final | Confiança |
| Média | Padronizar badge de tipo de trabalho (temporário / salvo / importado) | Expectativa de persistência |
| Baixa | Manter convencional só atrás de “Avançado” ou flag interna | Menos manutenção visual |

## Protótipo

Abra [`studio-voz-layout.html`](./studio-voz-layout.html) no navegador (arquivo estático, sem API). Há duas vistas interativas:

1. **Início** — escolha de origem  
2. **Workspace** — modelo + assistente de voz com estados (pausado / ouvindo / aplicado)

O protótipo usa as cores do Studio (`studio.css` dark) para facilitar comparação com a tela real.
