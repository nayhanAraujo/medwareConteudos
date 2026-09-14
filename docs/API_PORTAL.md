# Portal API MEDWARE CONTEUDO

## Acesso e arquitetura

O portal está em `/apiconteudos/docs`. Usa Nuxt/Vue/Pinia, o tema escuro isolado em `layouts/documentation.vue`, componentes em `components/documentation`, Bootstrap Icons e CSS responsivo. Não depende de um serviço externo de documentação.

O backend gera OpenAPI **3.0**, versão do contrato **1.0.0**. O catálogo inclui as 20 operações Python e dois recursos novos por cliente. Os 18 aliases legados em `/api/v1` e os dois OPTIONS explícitos continuam disponíveis. As integrações chamam os endpoints reais; o proxy documental não muda seus contratos.

- Parceiros: `/swagger/apiconteudos/swagger.json`, público.
- API Interna: `/swagger/api-v1/swagger.json`, JWT web administrativo.
- Web Admin: `/swagger/web/swagger.json`, JWT web administrativo.
- `/swagger` permanece como alternativa técnica de Parceiros, com execução apenas de GET.

Definição, ambiente, operação, aba e pesquisa ficam nos parâmetros `definicao`, `ambiente`, `operacao`, `aba`, `secao`, `q`. Guias são versionados em `frontend/nuxt-app/content/apiDocsGuides.ts`; métodos, parâmetros, respostas e esquemas vêm do OpenAPI.

## Configuração do servidor Nuxt

| Variável | Finalidade | Padrão local |
| --- | --- | --- |
| `NUXT_DOCS_API_BASE` | API atual, acessível pelo servidor Nuxt; também valida administrador | `http://localhost:5080` |
| `NUXT_DOCS_SANDBOX_BASE` | API de homologação isolada | `http://localhost:5081` |
| `NUXT_DOCS_PUBLIC_BASE` | Endereço público exibido nos exemplos do ambiente atual | `http://localhost:5080` |
| `NUXT_DOCS_SANDBOX_PUBLIC_BASE` | Endereço exibido para homologação | `http://localhost:5081` |
| `NUXT_DOCS_SANDBOX_INSTANCE_ID` | Identidade da cópia preparada; obrigatória para escrever | vazio, escrita bloqueada |
| `NUXT_DOCS_SUPPORT_URL` | Contato/canal de suporte HTTP(S) da organização | vazio |

Configure as variáveis **no processo Nuxt**, não apenas na API. Reinicie o servidor após mudanças. Não inclua credenciais nas URLs. Em implantação, configure também `Documentation:PortalUrl` na API para o endereço público real do portal; seu padrão é `http://localhost:3000/apiconteudos/docs`.

## Autenticação e segurança

O login web identifica o administrador que pode consultar definições restritas. O botão **Autorizar** configura separadamente a credencial de execução do ambiente/definição. Parceiros aceita colar JWT ou obtê-lo pela senha; Web Admin usa JWT emitido pelo login web do destino.

Em homologação, para abrir Interna/Web Admin, são necessárias **duas identidades**: login administrativo do sistema atual, validado pelo servidor, e JWT web administrativo da cópia, informado em Autorizar. Isso evita reutilizar os segredos de produção na cópia. A definição Interna não requer JWT de parceiro para executar seus aliases, mas sua documentação continua restrita.

Tokens do console vivem somente em memória e são apagados ao sair do portal; não vão para localStorage, cookies ou estado SSR. A autenticação web existente continua independente. Exemplos copiados substituem credenciais por placeholders.

O servidor Nuxt expõe `/api/documentacao/...`: configuração, contexto, definição e execução. Ele valida método/operação pelo OpenAPI, parâmetros, limites, identidade do destino e papel administrativo. Não aceita URL arbitrária, redirects, cookies de autenticação, cabeçalhos de proxy nem operações de integração externa. Limites: 10 MiB por requisição/resposta e 20 segundos de timeout.

**Experimentar** apenas abre o formulário. **Enviar requisição** executa. O ambiente atual permite consultas e emissão de token; demais escritas retornam 403 mesmo em chamadas diretas. A homologação exige identidade conferida em `/api/documentacao/contexto` e confirmação explícita. Amostras e respostas reais são separadas; arquivos binários oferecem download. Cancelar interrompe a espera, mas não desfaz uma transação já concluída pelo servidor.

## Reverse proxy

Encaminhar antes das regras genéricas de `/api` e `/apiconteudos`:

| Caminho | Destino |
| --- | --- |
| `/apiconteudos/docs` e variantes com query | Nuxt SSR |
| `/api/documentacao/*` | Nuxt SSR |
| `/_nuxt/*` | Nuxt/assets |
| `/apiconteudos/v1/*`, `/api/v1/*`, `/api/web/*`, `/swagger/*` | API .NET, conforme cutover aprovado |
| `/api-dotnet/*` | API .NET retirando `/api-dotnet`, conforme configuração existente |

Não servir este portal como exportação puramente estática: os handlers de segurança exigem **Nitro/Node em execução**. Não publicar JSONs restritos como arquivos estáticos nem colocá-los em cache público. Não alterar o tráfego dos parceiros antes da homologação funcional.

## Validação

```powershell
dotnet build backend/MdwConteudos.Api/MdwConteudos.Api.csproj --no-restore
dotnet test backend/ConversorHtml.Tests/ConversorHtml.Tests.csproj
cd frontend/nuxt-app
npm run test:docs
npm run build
```

Para verificação com dados, seguir [API_HOMOLOGACAO.md](API_HOMOLOGACAO.md). A matriz e as diferenças intencionais estão em [API_V1_COMPATIBILIDADE.md](API_V1_COMPATIBILIDADE.md).
