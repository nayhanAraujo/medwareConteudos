# Assistente — frases e exportação

Em `/assistente/frases`, escolha um grupo e abra uma frase para editar.
O editor visual oferece negrito, itálico, sublinhado, alinhamento, fonte,
cor e desfazer/refazer. O texto formatado é salvo em RTF; não é necessário
escrever os comandos RTF manualmente. Confira a formatação no editor e salve.

## Exportar um grupo

1. Selecione o grupo de frases.
2. Clique em **Exportar DOCX em ZIP**.
3. Abra o ZIP: há um documento por frase, com nome no formato
   `<id> - <título>.docx`. O identificador e o título ficam no nome do arquivo;
   o corpo contém apenas o texto da frase e sua formatação suportada, sem
   cabeçalho de identificação inserido pela aplicação.

A exportação usa todas as frases do grupo, não só a página visível. O ZIP é
montado no navegador e não cria arquivos no servidor. Imagens, tabelas e
objetos RTF incorporados não são suportados na exportação; se uma frase não
puder ser convertida, a tela informa o erro em vez de entregar um ZIP parcial.

Roteiro de verificação: edite uma frase de teste com acentos, quebras de linha
e formatação, salve e reabra. Exporte um grupo com mais de uma frase e confira
os nomes e o conteúdo dos documentos. As verificações de navegador ficam em
`frontend/nuxt-app/tests/phraseRtf.browser.mjs` e `phraseDocx.browser.mjs`.
