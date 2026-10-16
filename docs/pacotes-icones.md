# Icones dos pacotes

## Consumo pela API publica

`GET /apiconteudos/v1/pacotes` retorna `success`, `data`, `total` e `timestamp`.
Cada pacote possui `codpacote`, `nome`, `descricao`, `icone_url` e `icone_base64`.
`icone_base64` contem o valor completo do banco (`data:image/png;base64,...`),
pronto para usar no atributo src de uma imagem, sem outra requisicao.
A URL e relativa a origem da API (por exemplo,
`/apiconteudos/v1/pacotes/1/icone`); resolva usando a origem do servidor.
`icone_url` e `icone_base64` sao null quando o pacote nao possui imagem.

`GET /apiconteudos/v1/pacotes/{codpacote}/icone` devolve bytes PNG com
`Content-Type: image/png`. Envie `Authorization: Bearer <token>` em ambas as
requisicoes. Em navegadores, use fetch autenticado e uma URL de Blob para
mostrar a imagem. Pacote inexistente ou sem imagem retorna 404 JSON.
A imagem inclui ETag e Cache-Control private, no-cache: envie If-None-Match
com o ETag anterior para receber 304 sem corpo quando ela nao mudou.

As mesmas rotas existem em `/api/v1`. A listagem de scripts e o ultimo
verificado incluem `pacote_icone_url` e `pacote_icone_base64`, mesmo com
`incluir_arquivos=0`. Os campos sao null para scripts sem pacote ou pacote
sem icone. Os contratos aparecem em `/apiconteudos/docs` e Swagger.

## Preparacao do banco

Antes de iniciar a API atualizada, execute no diretorio backend/MdwConteudos.Api:

```powershell
dotnet run --no-launch-profile -- --migrate-package-icons
```

Use a mesma configuracao de ambiente e conexao Firebird da instancia de destino.
O comando adiciona PACOTES.ICONE_BASE64 (BLOB SUB_TYPE TEXT UTF8) e preenche os
sete pacotes conhecidos pelo nome, somente quando o campo estiver vazio.
Pode ser repetido sem sobrescrever imagens existentes. Nao altera os scripts.
Em publicacoes, execute `dotnet MdwConteudos.Api.dll --migrate-package-icons`.

Os endpoints existentes /api/web/cadastros/pacotes recebem e retornam
iconeBase64: data:image/png;base64,... (PNG de 128 x 128, ate 1 MB decodificado).
Null ou string vazia remove a imagem. O cadastro converte PNG, JPEG e WebP de
ate 1 MB para PNG antes de enviar. Os arquivos da galeria ficam em
frontend/nuxt-app/public/images/pacotes e sao incorporados tambem na API para
o preenchimento inicial, sem depender do frontend na migracao.

Assets criados com imagegen integrado. Prompt: sete pictogramas medicos de
traco fino, coloridos e transparentes: coracao rosa com ECG, ultrassom azul,
vasos vermelhos, olho roxo, bebe turquesa, maca verde e estetoscopio amarelo.
