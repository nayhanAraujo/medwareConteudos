# Cutover — Flask para Nuxt + .NET

## Estratégia

Para instalação independente na nova VM Azure Windows, sem Python, prevalece [PRODUCAO_IIS.md](PRODUCAO_IIS.md). A estratégia abaixo descreve o cenário histórico de convivência com Flask.

1. Manter **Flask** em produção (`C:\MedwareConteudo`, NSSM) até cada módulo ter paridade validada.
2. Validar a API pública em cópias isoladas de Firebird e arquivos, seguindo [API_HOMOLOGACAO.md](API_HOMOLOGACAO.md); não executar testes de escrita nos originais.
3. Redirecionar tráfego por módulo (reverse proxy ou IIS URL Rewrite).

## Escopo desta migração

- Alvo técnico: ASP.NET Core 10 + Nuxt 4, usando o mesmo banco e schema Firebird.
- Permanecem no legado: upload/importação `uploaddll`, automação E2E e os fluxos de IA (Agente PubMed/Grok, Oráculo/XML e conversor Azure OpenAI).
- Importação de variáveis por arquivo `.cs`, importação de impressos `.mrd`, anexos e arquivos de versões fazem parte da nova stack.
- Nenhum módulo deve ser direcionado definitivamente para a nova stack antes dos testes funcionais e da homologação.

## Proxy de exemplo (IIS / ARR)

| Rota migrada | Destino |
|--------------|---------|
| `/login`, `/api/web/*`, páginas Nuxt | `http://localhost:3000` (Nuxt) + API `http://localhost:5080` |
| Rotas explicitamente fora do escopo e módulos ainda não homologados | `http://localhost:5000` |
| `/apiconteudos/docs`, `/api/documentacao/*` | Nuxt SSR, antes das regras genéricas de API |
| `/apiconteudos/v1/*` (após validação) | `http://localhost:5080` |

## Pipeline Azure (rascunho)

Adicionar estágios ao `azure-pipelines.yml` **somente após** homologação:

1. `dotnet publish` → `backend/MdwConteudos.Api`
2. `npm ci && npm run build` → `frontend/nuxt-app`
3. NSSM: segundo serviço `MedwareConteudoApi` (Kestrel :5080)
4. Executar Nuxt SSR com `node .output/server/index.mjs`; o portal exige os handlers Nitro, não somente arquivos estáticos. Ver [API_PORTAL.md](API_PORTAL.md).

## Rollback

- Reverter regras de proxy para Flask.
- Parar serviço .NET sem alterar o banco (schema compartilhado, sem migração de dados).

## Checklist por módulo

- [ ] Endpoints .NET retornam JSON equivalente ao Flask
- [ ] Telas Nuxt cobrem fluxos críticos
- [ ] Testes manuais com usuários reais
- [ ] API parceiros: Postman/`API_PARCEIROS.md` aprovado

## Estado de implementação

As APIs e telas migradas podem ser consideradas prontas para iniciar validação somente quando `dotnet build`, `dotnet test` e `npm run build` estiverem aprovados. Isso não substitui testes com o Firebird real nem autoriza o cutover.
# Corte para nova VM IIS

Seguir [PRODUCAO_IIS.md](PRODUCAO_IIS.md): API IIS loopback:5080, Nuxt Node 22 WinSW, Firebird local e dados fora das releases. NSSM e deploy incremental abaixo são históricos.

Antes do corte: aceite em cópias isoladas, backup gbak+arquivos consistente, restore ensaiado, DNS/TTL planejados, validação sem Apply e smoke HTTPS. Suspender todos os escritores na origem na sincronização final; não manter duas bases divergentes recebendo gravações. Liberar tráfego somente após health live/ready e aceite de autenticação, documentação, anexos e parceiros. Rollback de código não restaura banco; seguir trilhas separadas documentadas.
