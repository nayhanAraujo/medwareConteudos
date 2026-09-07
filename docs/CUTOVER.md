# Cutover — Flask para Nuxt + .NET

## Estratégia

1. Manter **Flask** em produção (`C:\MedwareConteudo`, NSSM) até cada módulo ter paridade validada.
2. Validar a nova stack a partir da raiz do checkout contra o mesmo Firebird e `.env`.
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
| `/apiconteudos/v1/*` (após validação) | `http://localhost:5080` |

## Pipeline Azure (rascunho)

Adicionar estágios ao `azure-pipelines.yml` **somente após** homologação:

1. `dotnet publish` → `backend/MdwConteudos.Api`
2. `npm ci && npm run build` → `frontend/nuxt-app`
3. NSSM: segundo serviço `MedwareConteudoApi` (Kestrel :5080)
4. Servir Nuxt estático via IIS ou `node .output/server/index.mjs`

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
