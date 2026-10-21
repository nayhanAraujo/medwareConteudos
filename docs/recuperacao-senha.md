# Recuperação de senha por e-mail

O login oferece `/forgot-password`, que recebe o e-mail cadastrado. O link enviado abre `/reset-password#token=...`. O token é retirado da URL após a leitura, fica em memória e nunca é armazenado no navegador. Recarregar a página após a limpeza exige abrir novamente o link recebido.

## Configuração e implantação

1. Configure `PasswordRecovery:FrontendUrl` no backend com a URL pública HTTPS do frontend, por exemplo `https://conteudos.exemplo.com`. A variável de ambiente equivalente é `PasswordRecovery__FrontendUrl`. Apenas em Development é permitido HTTP em localhost. A URL pode incluir o caminho base da aplicação, sem query, fragmento ou credenciais.
2. Configure `SMTP_SERVER`, `SMTP_PORT` (padrão 587), `SMTP_USERNAME`, `SMTP_PASSWORD` e, opcionalmente, `SMTP_SENDER`. O remetente padrão é `SMTP_USERNAME`. São as mesmas configurações utilizadas pelos envios existentes; variáveis de ambiente têm prioridade. O envio usa STARTTLS, valida o certificado e tem limite de 15 segundos.
3. Com a API parada, antes de executar a nova versão contra o banco existente, aplique `dotnet run --no-launch-profile --project backend/MdwConteudos.Api -- --migrate-database`, com o ambiente e a conexão corretos. Na versão publicada, use `dotnet MdwConteudos.Api.dll --migrate-database`. A migração é repetível e adiciona `USUARIO.EMAIL`, seu índice único e `PASSWORD_RESET`. As consultas de usuários da nova versão dependem dessa migração. Não há preenchimento automático dos e-mails existentes.
4. Um administrador ativo deve cadastrar os endereços na tela de usuários. E-mail é opcional; aceita endereços ASCII de até 254 caracteres, normalizados com trim e letras minúsculas. Domínios internacionais podem ser cadastrados em punycode. Usuários sem e-mail continuam acessando normalmente e dependem do administrador para recuperar a senha.
5. Valide a entrega com uma conta controlada antes de publicar. Homologacao mantém a recuperação e os envios bloqueados; utilize Development com banco temporário e SMTP de teste.

## Contratos

- `POST /api/web/auth/forgot-password`: `{ "email": "pessoa@exemplo.com" }`. E-mail inválido retorna 400. Endereços válidos recebem 200 com `success` e a mesma `message`, inclusive se a conta não existir, estiver inativa ou o envio falhar.
- `POST /api/web/auth/reset-password`: `{ "token": "...", "newPassword": "...", "confirmPassword": "..." }`. Sucesso retorna 200. Token inválido, expirado ou utilizado retorna 400; senhas devem coincidir e ter pelo menos seis caracteres. Não cria sessão.
- Cadastro e edição de usuários aceitam `email`. Omitir o campo ou enviar `null` na edição preserva o endereço existente. Enviar `""` limpa o endereço. Apenas administrador ativo pode alterá-lo; o frontend omite o campo para os demais usuários.

## Regras operacionais

O token contém 32 bytes aleatórios e é armazenado apenas como SHA-256. Expira em 30 minutos. Nova emissão invalida os links anteriores. Redefinição, troca de senha pelo administrador, alteração de e-mail ou inativação da conta também invalidam os links. Consumo do token e atualização de senha ocorrem na mesma transação, serializada pela conta no Firebird.

Solicitação e redefinição compartilham cinco requisições por IP/minuto. Excesso retorna 429 com `Retry-After: 60`. A emissão tem intervalo de um minuto por conta, controlado no banco. A limitação por IP pertence à instância da API; uma instalação com múltiplas réplicas precisa de limite compartilhado no proxy. A mensagem ao solicitar é sempre “Se houver uma conta ativa com esse e-mail, enviaremos as instruções de recuperação.”

Falhas SMTP registram somente `PasswordRecoveryEmail Outcome=failed`, sem destinatário, senha, token ou exceção SMTP. Verifique esse evento e as configurações quando não houver entrega. O token do envio malsucedido é invalidado. Tokens de autenticação já emitidos continuam sujeitos à validade normal de sessão, como nas alterações administrativas de senha existentes.

## Verificação

Os testes `PasswordRecoveryTests`, `AccountSecurityTests`, `ProductionHttpSecurityTests` e `PortalOpenApiTests` verificam validação, autorização, confirmação genérica, falha de envio, limitação e contratos. `PasswordRecoveryFirebirdTests` usa banco embedded temporário, verifica migração repetida, unicidade, invalidação, expiração, consumo simultâneo e login com a nova senha. `RecoveryEmailSenderTests` recebe o e-mail em um servidor SMTP local com STARTTLS, certificado e credenciais exclusivos do teste, sem envio externo.

No PowerShell, configure `PUBLICACAO_TEST_FBCLIENT` para o `fbclient.dll` do Firebird de teste e execute `dotnet test backend/ConversorHtml.Tests`. O banco da aplicação não é utilizado por esses testes. No frontend, execute `node --experimental-strip-types --test tests/passwordRecovery.test.ts` e `npm run build` dentro de `frontend/nuxt-app`.
