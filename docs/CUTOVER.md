# Cutover — Flask para Nuxt + .NET

## Estratégia

1. Manter **Flask** em produção (`C:\MedwareConteudo`, NSSM) até cada módulo ter paridade validada.
2. Validar a nova stack em `mdw-migracao/` contra o mesmo Firebird e `.env`.
3. Redirecionar tráfego por módulo (reverse proxy ou IIS URL Rewrite).

## Proxy de exemplo (IIS / ARR)

| Rota migrada | Destino |
|--------------|---------|
| `/login`, `/api/web/*`, páginas Nuxt | `http://localhost:3000` (Nuxt) + API `http://localhost:5080` |
| Demais rotas Flask | `http://localhost:5000` |
| `/apiconteudos/v1/*` (após validação) | `http://localhost:5080` |

## Pipeline Azure (rascunho)

Adicionar estágios ao `azure-pipelines.yml` **somente após** homologação:

1. `dotnet publish` → `mdw-migracao/backend/MdwConteudos.Api`
2. `npm ci && npm run build` → `mdw-migracao/frontend/nuxt-app`
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
