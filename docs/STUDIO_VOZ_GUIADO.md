# Edição por voz guiada e proteção de dependências

A nova apresentação de `/studio/voz` é opcional. Na configuração da API, habilite:

```json
"StudioVoiceUx": {
  "Enabled": true
}
```

O valor padrão é `false`: a tela anterior continua disponível. A consulta autenticada
`GET /api/voice/capabilities` informa a disponibilidade da apresentação, dos rascunhos
persistentes e da conversação contínua, sem retornar credenciais. A configuração
`RealtimeVoice` continua independente. A reformulação do layout usa a consulta
existente e pode ser publicada no frontend com uma API que já a ofereça.

## Roteiro de teste para o usuário

1. Entre no Studio e abra **Voz**. Escolha **Novo modelo**, **Abrir modelo**,
   **Importar arquivo** (JSON) ou **Usar imagem**. Abrir uma cópia não modifica o modelo original.
2. Se existir um trabalho anterior, abra-o em **Continuar de onde parou**. Confira
   o nome e o estado de salvamento. Sem armazenamento persistente, a tela informa
   **Trabalho temporário — baixe o modelo antes de sair**.
3. Confira que o microfone não abriu sozinho. Clique em **Falar alteração**, diga
   “Crie uma seção chamada Dados do paciente” e clique em **Parar gravação**.
   Confira que a frase aparece e que o modelo só muda após **Aplicar alteração**.
   Repita para adicionar Altura. A etapa **Revisar** deve destacar as mudanças.
4. Em **Opções avançadas**, ative **Conversação contínua** quando disponível e
   peça uma medida que possua nomes semelhantes no catálogo. As sugestões devem
   permanecer no painel. Selecione a medida e a seção e clique em **Incluir medida**.
   Nenhuma candidata deve entrar antes desse clique. Teste também **Cancelar sugestões**.
   No trabalho temporário, confirmar a escolha encerra a conexão de áudio para aplicar
   a ação com segurança; inicie novamente o microfone quando desejar continuar.
5. No editor integrado, selecione uma seção e confira **Seção selecionada**. Use
   **Adicionar medida** para abrir o banco flutuante; teste busca, arraste da janela,
   inclusão por botão e organização sem arrastar.
6. Inclua Superfície Corporal com Peso e Altura. Tente remover Peso e depois Altura.
   A remoção deve ser recusada, com os nomes completos dos cálculos dependentes.
   O aviso permanece até **Entendi** ou **Ver medidas dependentes**; este último
   localiza o item. Quantidade, ordem e revisão do documento devem permanecer iguais.
7. No editor integrado e em `/studio/modelos`, tente esvaziar/excluir a seção de
   Peso enquanto Superfície Corporal estiver em outra seção. Toda a operação deve
   ser recusada. Remover uma medida calculada que ninguém usa deve funcionar.
8. Importe um JSON com funções auxiliares. Uma função preservada que usa a medida
   também deve impedir sua remoção. Seus detalhes são somente leitura; scripts
   importados não são executados na tela. Pendências anteriores sem relação com
   a remoção não devem bloquear a edição.
9. Na conversação contínua, pause e retome o microfone. Interrompa a rede durante
   uma consulta e confirme que o trabalho permanece. Volte à gravação por etapas e
   confirme que é possível editar a frase ou enviar áudio sem aplicá-lo automaticamente.
10. Confira **Salvar modelo** e **Baixar modelo** no cabeçalho integrado. Para JSON
    importado, somente rascunho e download JSON completo ficam disponíveis. Em
    trabalhos temporários, faça o download antes de sair. Cancele o seletor de formato
    e confira que a etapa **Baixar** só é concluída quando o download é iniciado.
11. Teste Tab, Shift+Tab, Enter e Escape; confira foco no aviso e seu retorno ao
    botão original. No celular, teste os botões das colunas e inclusão sem arrastar.
12. Com dois separadores abertos, provoque uma mudança de revisão enquanto as
    sugestões estão visíveis. A escolha antiga deve pedir cancelamento e nova
    solicitação. Uma falha de rede seguida de repetição não deve duplicar a medida.
13. No trabalho temporário, arraste uma medida e solte sobre outra para posicioná-la
    antes dela. Solte na área tracejada da seção para colocá-la no fim, inclusive
    em seções vazias da outra coluna. Repita com um JSON importado e baixe o arquivo
    para conferir nomes, tokens, fórmulas e propriedades preservadas. Use **Desfazer**
    e **Refazer** no trabalho importado para conferir o histórico do movimento.
14. Use **Mover medida**, **Subir**, **Descer** e o seletor de seção com teclado ou
    no celular. Durante gravação, conexão contínua ou confirmação pendente, os
    movimentos ficam indisponíveis. Encerre a conexão ou resolva a operação antes
    de editar. Uma falha de rede deve permitir repetir a mesma operação sem duplicá-la.

O endpoint de edição manual temporária aceita a operação `move`, um item em
`itemIds`, `sectionId` e `beforeItemId` opcional. A ausência do último campo move
para o fim. Os IDs das seções e âncoras podem ser os IDs preservados do arquivo.
O servidor valida propriedade, versão, destino e vínculo da âncora antes de aplicar.
O movimento usa o sincronizador JSON existente e o histórico de importados.
No modelo temporário convencional, o JSON é adaptado para o comando estrutural
durante a operação e continua armazenado na sessão temporária.

## Contrato de remoção

A regra compartilhada compara a operação completa com o documento que permanecerá.
Consulta dependências atuais do catálogo e funções efetivamente preservadas dos
JSONs importados. Não há exclusão em cascata nem remoção forçada.

As recusas retornam HTTP `409`, `code: "measure_in_use"`, mensagem e `details`
com `removed` e `dependents`. Estes incluem nomes, identificadores, seção, referência
necessária e indicação de dependência indireta. A recusa não grava revisão nem
histórico aplicado. O cliente a apresenta como bloqueio de negócio, sem guardar
o comando para reenvio automático. As rotas convencionais validam antes das escritas
e serializam as alterações por bloqueio transacional do modelo.

A remoção continua manual. Não foi adicionada ferramenta de remoção por voz.

## Validações automatizadas

Na pasta `frontend/nuxt-app`, execute:

```powershell
node --test tests/studioApiErrors.test.mjs tests/studioTemporaryVoice.test.mjs tests/studioDependencies.test.mjs tests/studioDraft.test.mjs tests/studioChoice.test.mjs tests/realtimeVoice.test.mjs tests/studioJsonImport.test.mjs tests/voiceSession.test.mjs tests/realtimePcm.test.mjs
```

Os testes C# `StudioDependencyGuardTests`, `StudioJsonImportTests` e
`StudioTemporaryRemovalTests` e `RealtimeChoiceGateTests` cobrem proteção de dependências, preservação e espera por
escolha. Sua execução depende da compilação manual do projeto de testes.
Microfone real, xAI, permissões de usuários, leitores de tela, conexão IIS e
transações Firebird devem ser conferidos no ambiente de validação. Esta entrega
não executa migrações, commit, push ou deploy.
