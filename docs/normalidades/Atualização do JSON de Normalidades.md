# Atualização do JSON de Normalidades Ecocardiográficas

## Entrega

Gerar `NormalidadesEcodopplercardiogramaMedware.atualizado.json` na pasta Documentos, preservando o original. Revisar as 58 medidas existentes e acrescentar as medidas faltantes alinhadas nesta conversa.

O banco `mdw-migracao\bd\REFERENCIAS.FDB` será consultado somente para leitura. A conexão e as três tabelas foram confirmadas. Projetos, MRDs e importadores não serão alterados nesta etapa.

## Estrutura e Identificação

- Preservar as chaves existentes, os blocos por sexo, `COMENTARIOTEXTO`, `_meta` e `VARIAVEISALTERNATIVAS`.
- Acrescentar `NOMEALTERNATIVO` como lista de nomes clínicos, provenientes de `VARIAVEIS.NOME` e `VARIAVEISNOMESCLINICOS.NOME`, relacionados por `CODVARIAVEL`.
- Preencher `VARIAVEISALTERNATIVAS` com os identificadores de `VARIAVEISALTERNATIVAS.ALTERNATIVA`, distinguindo códigos de nomes clínicos.
- Eliminar duplicatas e espaços excedentes. Não associar medidas apenas pela semelhança do nome; conferir anatomia, método e unidade.
- Para novos itens, utilizar o código do banco quando houver correspondência inequívoca. Correspondências ausentes ou ambíguas serão registradas para revisão.
- Acrescentar unidade, código do banco e situação de verificação aos metadados.

## Revisão Científica

- Pesquisar fontes primárias publicadas até a data da execução, priorizando diretrizes aplicáveis e verificando atualizações posteriores às fontes de 2015, 2017, 2024 e 2025 discutidas.
- Conferir cada valor diretamente na tabela ou no texto original, incluindo unidade, sexo, método de aquisição e população.
- Registrar `_meta.Ano`, `_meta.Fonte` com título completo, `_meta.Pagina` com a página impressa e campos adicionais para página do PDF, tabela/figura, DOI e link oficial.
- Manter referências anteriores quando ainda forem aplicáveis. Um estudo mais recente não substituirá automaticamente uma diretriz por tratar de população ou técnica diferente.
- Incluir os parâmetros diastólicos faltantes e demais medidas com normalidades discutidas, após conferir os códigos e as fontes.
- Tratar valores anteriormente solicitados pela médica como candidatos à conferência. Divergências científicas serão documentadas, sem atribuir respaldo inexistente.
- Remover graus leve, moderado e grave quando a fonte não os estabelecer. Medidas sem faixa universal comprovada permanecerão identificadas, com explicação e sem intervalo numérico inventado.

## Limites e Compatibilidade

- Preservar `min` e `max`, permitindo `null` no extremo que a fonte não definir.
- Adicionar `minInclusivo` e `maxInclusivo` para distinguir `<`, `<=`, `>` e `>=`; usar `null` no indicador correspondente a um extremo ausente.
- Não usar `999`, zero ou pequenos incrementos decimais para simular limites não publicados.
- Distinguir intervalo de referência de ponto de corte diagnóstico nos metadados e comentários, especialmente para E/A e E/e’.
- Manter a convenção de sinal do strain explicitamente documentada.
- Gerar um relatório Markdown junto ao JSON com alterações, pendências e requisitos de compatibilidade. O importador atual exige dois extremos numéricos e ignora `NOMEALTERNATIVO`; sua adaptação ficará documentada para uma etapa posterior.

## Validação

- Validar sintaxe, chaves únicas, tipos dos campos, listas de sinônimos e preservação das 58 medidas originais.
- Conferir limites inclusivos e exclusivos, faixas invertidas, sobreposições indevidas, conversões de unidades e valores negativos.
- Verificar a correspondência entre código, nome clínico, unidade e método no banco.
- Conferir estudo, ano, página e tabela de cada faixa numérica publicada no novo arquivo.
- Comparar o JSON final com o original e registrar inclusões, correções e graus removidos.
- Confirmar que o JSON original permaneceu intacto e que nenhuma escrita foi realizada no banco.
