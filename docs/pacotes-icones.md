# Icones dos pacotes

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
