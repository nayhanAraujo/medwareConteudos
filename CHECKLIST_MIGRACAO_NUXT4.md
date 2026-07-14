# Checklist — Migração Nuxt 3 → Nuxt 4 + Tailwind 4 (MDW Conteúdos)

**Projeto:** `mdw-migracao/frontend/nuxt-app`  
**Repo Azure:** `Conteudos_migracao`  
**Antes:** Nuxt `^3.21.6` + Tailwind `^3.4` (`@nuxtjs/tailwindcss`)  
**Alvo:** Nuxt `^4.4.x` + Tailwind `^4` via `@tailwindcss/vite`  
**Branch:** `chore/upgrade-nuxt4-tailwind4`  
**Guia Nuxt:** https://nuxt.com/docs/getting-started/upgrade  
**Guia Tailwind Nuxt:** https://tailwindcss.com/docs/installation/framework-guides/nuxt  

---

## Princípios

1. Upgrade em branch dedicada (`chore/upgrade-nuxt4-tailwind4`) — Nuxt 4 + Tailwind 4 no mesmo PR.
2. Design System: tokens em `assets/css/tailwind.css` (`@theme`), **sem Preflight** (Bootstrap).
3. Depois do merge, todos fazem `git pull` + limpar `node_modules`/`.nuxt` + `npm install`.
4. Grande parte do código Vue/composables **já é compatível**; não é “refatorar tudo”.

---

## 0. Alinhamento (antes de qualquer commit)

- [ ] Combinar com o time: “a partir do merge deste PR, o frontend oficial é Nuxt 4”
- [ ] Ninguém abre PR grande paralelo na pasta `frontend/nuxt-app` durante o upgrade
- [ ] Backlog: features novas só após o merge do upgrade (ou rebase nele)
- [ ] Atualizar documentação (`MIGRACAO_GIT_WORKFLOW.md` / README do `nuxt-app`) com a versão

---

## 1. Branch e baseline limpa

```bash
cd mdw-migracao
git checkout main
git pull origin main
git checkout -b chore/upgrade-nuxt4-tailwind4
cd frontend/nuxt-app
```

- [x] Partir da `main` **já com** Relatórios/Painéis (`bb55f27` ou posterior)
- [x] Branch `chore/upgrade-nuxt4-tailwind4` criada
- [ ] Se houver alterações locais não relacionadas: stash ou outra branch

---

## 2. Snapshot do que existe hoje (inventário MDW)

### Estrutura atual (Nuxt 3 clássico — na raiz do app)

| Pasta / arquivo | Observação |
|-----------------|------------|
| `app.vue`, `pages/`, `components/`, `composables/`, `layouts/`, `middleware/`, `plugins/`, `stores/`, `utils/`, `assets/` | Código da app na raiz |
| `server/` | Quase vazio (só `tsconfig.json`) — pouco impacto |
| `nuxt.config.ts` | Pinia, Tailwind, proxy Nitro, CSS Bootstrap + DS |
| `useApi.ts` | Usa `fetch` nativo + `runtimeConfig.public.apiBase` — baixo risco Nuxt 4 |
| Módulos: `@pinia/nuxt` + `@tailwindcss/vite` | Tailwind 4 sem módulo `@nuxtjs/tailwindcss` |

### Módulos de negócio a validar após o upgrade

- [ ] Auth / login / middleware `auth.global.ts` e `admin.ts`
- [ ] Conteúdos (`pages/conteudos`)
- [ ] Scripts (`pages/scripts`)
- [ ] Variáveis (`pages/variaveis`)
- [ ] Relatórios (`pages/relatorios`) — código do Adriano
- [ ] Painéis (`pages/paineis`)
- [ ] Biblioteca / Referências / Usuários / Agente
- [ ] Design System `components/ds/*` + tokens Tailwind

---

## 3. Upgrade de dependências

### Opção recomendada (oficial)

```bash
cd frontend/nuxt-app
npx nuxt upgrade --dedupe
```

### Feito neste upgrade

- [x] `"nuxt": "^4.4.8"`
- [x] Remover `@nuxtjs/tailwindcss` e `tailwindcss@3`
- [x] Instalar `tailwindcss@^4` + `@tailwindcss/vite`
- [x] `npm install` + lock commitado
- [x] `npx nuxt prepare` sem erro

### Módulos / dependências

| Pacote | Ação |
|--------|------|
| `nuxt` | ^4.x |
| `@pinia/nuxt` + `pinia` | Peer deps com Nuxt 4 |
| `tailwindcss` + `@tailwindcss/vite` | ^4.x (oficial Nuxt guide) |
| `@nuxtjs/tailwindcss` | **Removido** |
| `bootstrap` / ícones CDN | Validar visual (Preflight off) |
| `sweetalert2` | Validar `useSwal()` |

---

## 4. Configuração (`nuxt.config.ts` + Tailwind CSS)

- [x] Remover `@nuxtjs/tailwindcss` de `modules` e bloco `tailwindcss: {}`
- [x] `vite.plugins: [tailwindcss()]` com `@tailwindcss/vite`
- [x] CSS: Bootstrap → `tailwind.css` → `app-layout` → `scripts`
- [x] Tokens DS em `assets/css/tailwind.css` (`@theme`), Preflight desligado
- [x] Remover `tailwind.config.ts` (config CSS-first)
- [ ] Validar proxy `/api-dotnet` e assets `/static` + `/uploads`
- [ ] Rodar `npm run dev` e conferir páginas autenticadas

---

## 5. Estrutura de pastas `app/` (opcional na 1ª entrega)

Nuxt 4 **permite** manter a estrutura atual (raiz). Migração para `app/` pode ser **fase 2**.

### Fase 1 (mínima — recomendada para o primeiro PR)

- [ ] Manter pastas onde estão (`pages/`, `components/`, etc. na raiz)
- [ ] Nuxt auto-detecta layout legado
- [ ] Foco: versão + build + smoke tests

### Fase 2 (depois, se o time quiser)

Mover para dentro de `app/`:

- [ ] `app.vue`, `pages/`, `components/`, `composables/`, `layouts/`, `middleware/`, `plugins/`, `assets/`
- [ ] Manter na **raiz**: `nuxt.config.ts`, `server/`, `public/`, `shared/` (se criar), `package.json`
- [ ] Tema Tailwind continua em `assets/css/tailwind.css` (ou mover com assets)
- [ ] Revalidar imports `~/` e aliases
- [ ] PR separado: `chore/nuxt4-app-directory`

---

## 6. TypeScript e qualidade

- [ ] `npx nuxi typecheck` (ou `vue-tsc` se configurado)
- [ ] Corrigir erros de tipo que o Nuxt 4 passar a expor
- [ ] Conferir stores Pinia (`stores/`) e auto-imports
- [ ] Conferir middleware global de auth

---

## 7. Build e runtime

```bash
npm run build
npm run preview   # opcional
```

- [ ] `npm run build` sem erro
- [ ] Pasta `.output` gerada
- [ ] Dev: `npm run dev` sobe em `:3000`
- [ ] API .NET rodando em `:5080` em paralelo

---

## 8. Smoke test funcional (obrigatório antes do PR)

Com API + Nuxt no ar:

### Auth / shell

- [ ] Login (dev / Firebird)
- [ ] Menu / layout / logout
- [ ] Sessão expirada redireciona com SweetAlert (`useApi` → 401)

### Núcleo MDW

- [ ] Hub Conteúdos
- [ ] Scripts: listagem cards/lista, detalhe, ações
- [ ] Variáveis: listagem / grid
- [ ] Relatórios: listagem, criar/editar (módulo Adriano)
- [ ] Painéis: listagem e formulário
- [ ] Biblioteca / Referências (navegação básica)
- [ ] Uploads / imagens / static carregando (`/static`, `/uploads` se usado)

### Design System

- [ ] `DsPageHeader`, `DsPageShell`, `DsButton`, `DsModal`, alertas
- [ ] Sem regressão forte de CSS (Bootstrap + Tailwind, preflight off)

---

## 9. Git / PR

```bash
# ainda em chore/upgrade-nuxt4
git add frontend/nuxt-app/package.json frontend/nuxt-app/package-lock.json
# + nuxt.config / outros arquivos só se o upgrade/codemod alterou
git commit -m "chore: upgrade Nuxt 3 para Nuxt 4"
git push -u origin chore/upgrade-nuxt4
```

- [ ] Abrir PR no Azure: `chore/upgrade-nuxt4` → `main`
- [ ] Descrição do PR: versão antiga → nova, checklist de smoke test, modules validados
- [ ] Review de pelo menos 1 pessoa do time (idealmente Adriano)
- [ ] Merge
- [ ] Comunicar equipe: `pull` + `npm install` em `frontend/nuxt-app`
- [ ] Apagar branch local e remota após o merge

---

## 10. Pós-merge (cada desenvolvedor)

```bash
cd mdw-migracao
git checkout main
git pull origin main
cd frontend/nuxt-app
rm -rf node_modules .nuxt   # PowerShell: Remove-Item -Recurse -Force node_modules, .nuxt
npm install
npm run dev
```

- [ ] Confirmar `package.json` com `"nuxt": "^4..."`
- [ ] App sobe localmente
- [ ] Features novas em **branches novas** a partir desta `main`

---

## 11. O que NÃO fazer neste PR de upgrade

- [ ] Não refatorar páginas de Relatórios/Scripts “porque eram Nuxt 3”
- [ ] Não subir Nuxt 4 junto com ajuste de `VariaveisActionsLegend` / feature de UI
- [ ] Não pedir ao Adriano para reenviar Relatórios só por causa da versão
- [ ] Não migrar tudo para pasta `app/` no mesmo PR (se não for estritamente necessário)

---

## 12. Riscos específicos deste projeto

| Risco | Mitigação |
|-------|-----------|
| Proxy `/api-dotnet` quebrar no Nitro 4 | Smoke test login + listagens |
| CSS Bootstrap + Tailwind Preflight | Importar só `theme` + `utilities` em `tailwind.css` |
| Tokens DS (`bg-ds-*`, `shadow-ds`, etc.) | Portados para `@theme` em `assets/css/tailwind.css` |
| `package-lock` / node_modules antigo | Limpar `.nuxt` e `node_modules`, reinstalar |
| Time em versões diferentes | Após merge: `pull` + `npm install` obrigatório |

---

## 13. Ordem sugerida de trabalho do time

```
1. Branch/PR chore/upgrade-nuxt4-tailwind4 → validar local → merge main
2. Todos: pull + limpar node_modules/.nuxt + npm install
3. Features novas a partir desta main
4. (Opcional) PR chore/nuxt4-app-directory
```

---

## Referências

- Upgrade guide: https://nuxt.com/docs/getting-started/upgrade  
- Announcement Nuxt 4: https://nuxt.com/blog/v4  
- Tailwind + Nuxt: https://tailwindcss.com/docs/installation/framework-guides/nuxt  
- Fluxo Git do projeto: `CONTEUDOS/MIGRACAO_GIT_WORKFLOW.md`  

---

## Status rápido (marcar quando concluir)

| Etapa | Status | Data / responsável |
|-------|--------|--------------------|
| Branch `chore/upgrade-nuxt4-tailwind4` | ☑ | Nayhan |
| Nuxt 4 + Tailwind 4 nas deps | ☑ | Nayhan |
| Tema DS em `tailwind.css` | ☑ | Nayhan |
| Build OK | ☑ | Nayhan |
| Smoke test OK | ☑ | Nayhan (login 200 + tokens DS no CSS) |
| Push / merge main | ☑ | Nayhan (`12d5280`) |
| Time sincronizado | ☐ | |
