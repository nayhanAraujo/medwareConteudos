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
`RealtimeVoice` continua independente. Atualize API e frontend juntos; a compilação
permanece manual. Não há alteração de esquema nem migração nesta entrega.

## Roteiro de teste para o usuário

1. Entre no Studio e abra **Voz**. Escolha **Novo modelo**, **Abrir modelo**,
   **Importar JSON** ou **Usar imagem**. Abrir uma cópia não modifica o modelo original.
2. Se existir um trabalho anterior, abra-o em **Continuar um trabalho**. Confira
   o nome e o estado de salvamento. Sem armazenamento persistente, a tela informa
   **Trabalho temporário — baixe o modelo antes de sair**.
3. Confira que o microfone não abriu sozinho. Clique em **Iniciar edição por voz**.
   Diga “Crie uma seção chamada Dados do paciente”. Depois peça “Adicione Altura
   em Dados do paciente”. O modelo deve permanecer visível enquanto você fala.
4. Peça uma medida que possua nomes semelhantes no catálogo. As sugestões devem
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
9. Pause e retome o microfone. Interrompa a rede durante uma consulta: o trabalho
   deve permanecer. Use **Usar gravação por etapas** para escolher o outro modo;
   não deve haver repetição automática de áudio.
10. Confira **Salvar modelo** e **Baixar modelo** no cabeçalho integrado. Para JSON
    importado, somente rascunho e download JSON completo ficam disponíveis. Em
    trabalhos temporários, faça o download antes de sair.
11. Teste Tab, Shift+Tab, Enter e Escape; confira foco no aviso e seu retorno ao
    botão original. No celular, teste os botões das colunas e inclusão sem arrastar.
12. Com dois separadores abertos, provoque uma mudança de revisão enquanto as
    sugestões estão visíveis. A escolha antiga deve pedir cancelamento e nova
    solicitação. Uma falha de rede seguida de repetição não deve duplicar a medida.

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
