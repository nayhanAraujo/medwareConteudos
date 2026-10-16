# Icones dos modulos de relatorios

## API de conteudos

`GET /apiconteudos/v1/relatorios` retorna `modulo_icone_url` e
`modulo_icone_base64` em cada relatorio,
preservando os demais campos. Exemplo:
`/apiconteudos/v1/relatorios/modulos/1/icone`. Resolva a URL relativa usando
a origem da API. O Base64 e a data URI completa armazenada no banco
(`data:image/png;base64,...`), pronta para usar no src de uma imagem sem
outra requisicao. Ambos os campos sao null quando nao ha modulo ou imagem.
O vinculo por CODMODULO tem prioridade; se esse codigo nao corresponder a um
modulo existente, o nome legado e comparado sem diferenca de caixa e espacos
nas extremidades. Se nomes duplicados existirem, usa o menor CODMODULO.
Um modulo vinculado por codigo sem imagem retorna null, sem trocar de modulo.

`GET /apiconteudos/v1/relatorios/modulos/{codmodulo}/icone` retorna os bytes
PNG com Content-Type image/png e Content-Disposition attachment com nome
`modulo-{codmodulo}.png`. Envie o mesmo JWT de parceiro no header
Authorization: Bearer <token>, inclusive para baixar a imagem. Para mostrar
em navegador, use fetch autenticado e crie uma URL de Blob para o src.
O download retorna ETag e Cache-Control private, no-cache. If-None-Match com
o ETag atual retorna 304 sem corpo; modulo inexistente ou sem imagem retorna
404 JSON. Ambas as rotas possuem alias `/api/v1` e aparecem no Swagger e em
`/apiconteudos/docs`. Filtros da listagem continuam disponiveis.

## Preparacao do banco

Antes de iniciar a API atualizada, execute na pasta da API publicada, com a
configuracao do banco de destino:

```powershell
dotnet MdwConteudos.Api.dll --migrate-report-module-icons
```

O comando adiciona MODULORELATORIO.ICONE_BASE64 como BLOB SUB_TYPE TEXT UTF8
nullable e pode ser repetido. Alternativa SQL (executar uma unica vez):

```sql
ALTER TABLE MODULORELATORIO ADD ICONE_BASE64 BLOB SUB_TYPE TEXT CHARACTER SET UTF8;
COMMIT;
```

Em Relatorios > Modulos, use Editar modulo para escolher um simbolo da galeria
ou enviar PNG, JPEG ou WebP de ate 1 MB. O navegador converte para PNG 128x128.
Salvar grava a data URI completa em base64. Remover grava NULL e restaura o
simbolo padrao por nome. A galeria reutiliza os Bootstrap Icons ja usados no
sistema, convertidos em PNG no navegador.

POST e PUT /api/web/relatorios/modulos aceitam iconeBase64; a listagem completa
retorna o mesmo campo. A mudanca nao altera os arquivos dos relatorios nem os
vinculos dos sistemas quando se mantem a selecao atual.
