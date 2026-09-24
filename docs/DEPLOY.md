# Deploy e requisitos de runtime — MDW Conteúdos (Nuxt + .NET)

Para a nova VM Azure com IIS, siga **[PRODUCAO_IIS.md](PRODUCAO_IIS.md)**, que substitui as instruções de produção abaixo. Este documento conserva referências de desenvolvimento/implantação legada; `.env` não é carregado no ambiente Production. Complementa o [README.md](../README.md) e o [CUTOVER.md](CUTOVER.md).

---

## Visão geral

O portal `/apiconteudos/docs` exige Nuxt SSR/Nitro e regras específicas antes dos prefixos genéricos: [configuração do portal](API_PORTAL.md). Para testes de escrita, usar exclusivamente [homologação isolada](API_HOMOLOGACAO.md).

| Componente | Pasta | Porta padrão | Função |
|------------|-------|--------------|--------|
| API ASP.NET Core | `backend/MdwConteudos.Api` | **5080** | REST (`/api/web`, `/api/conversions`, `/apiconteudos/v1`) |
| Frontend Nuxt | `frontend/nuxt-app` | **3000** (dev) | Hub + `/studio` (conversor HTML ou TXT) |
| Bridge Node (Studio) | `backend/agent-bridge` | — | Conversão imagem → HTML ou TXT via `@cursor/sdk` |
| Firebird REFERENCIAS | `.env` / `Firebird:*` | 3052 | Login, scripts, cadastros web |
| Firebird ASSISTENTE | `AssistantFirebird:*` | 3050 | Módulo `/assistente/*` e publicação sincronizada |

Em produção, o Nuxt costuma ser servido como build estático ou SSR (`node .output/server/index.mjs`), com **reverse proxy** encaminhando `/api-dotnet` para a API .NET (o proxy do `nuxt.config.ts` só vale em `npm run dev`).

---

## Pré-requisitos

| Software | Versão mínima | Onde é usado |
|----------|---------------|--------------|
| **.NET SDK** | 10.x | Compilar e publicar `MdwConteudos.Api` |
| **Node.js** | **22+** | Obrigatório para `backend/agent-bridge` (`@cursor/sdk`) |
| **Node.js** | 18+ | Suficiente só para `frontend/nuxt-app` (recomendado **22+** em todo o servidor) |
| **npm** | 9+ | Instalar dependências frontend e bridge |
| **Firebird** | 3.x+ | Bancos REFERENCIAS e ASSISTENTE acessíveis pela API |

No servidor onde roda o **Studio com conversão real**, o executável `node` precisa estar no **PATH** do processo que inicia a API .NET (a API invoca `node convert.mjs` como subprocesso).

---

## Instalação de pacotes (obrigatório)

Execute **três** instalações npm distintas, além do restore .NET:

### 1. Frontend Nuxt

```powershell
cd frontend\nuxt-app
npm install
```

Produção (lockfile commitado):

```powershell
npm ci
npm run build
```

### 2. Bridge do conversor (Studio)

**Sem este passo, a conversão falha** com `Cannot find package '@cursor/sdk'`.

```powershell
cd backend\agent-bridge
npm install
```

Produção:

```powershell
npm ci --omit=dev
```

> `node_modules` não vai para o git (`.gitignore`). Após cada deploy, rode `npm ci` em `backend/agent-bridge` no servidor **ou** copie a pasta `node_modules` junto com o publish.

### 3. API .NET (restore)

```powershell
dotnet restore backend\MdwConteudos.slnx
dotnet build backend\MdwConteudos.slnx -c Release
```

Publicação:

```powershell
dotnet publish -c Release -o C:\caminho\publish\api
```

---

## Configuração

### Arquivo `.env` (raiz do repositório)

A API sobe o `.env` da raiz automaticamente ([`EnvFileLoader.cs`](../backend/MdwConteudos.Api/Infrastructure/EnvFileLoader.cs)). O arquivo **não** é versionado.

Variáveis comuns:

| Variável | Descrição |
|----------|-----------|
| `FIREBIRD_HOST` | Host do banco REFERENCIAS |
| `FIREBIRD_PORT` | Porta (ex.: `3052`) |
| `FIREBIRD_DB` | Caminho absoluto do `.FDB` REFERENCIAS |
| `FIREBIRD_USER` | Usuário Firebird |
| `FIREBIRD_PASSWORD` ou `LOCAL_DB_PASSWORD` | Senha |
| `API_JWT_SECRET` | Segredo JWT API parceiros |
| `API_JWT_PASSWORD` | Senha de parceiro |
| `CURSOR_API_KEY` | Chave User API do Cursor (Studio) |

### Banco Assistente (prefixo `ASSISTENTE_FIREBIRD_`)

| Variável | Descrição |
|----------|-----------|
| `ASSISTENTE_FIREBIRD_HOST` | Host |
| `ASSISTENTE_FIREBIRD_PORT` | Porta (ex.: `3050`) |
| `ASSISTENTE_FIREBIRD_DATABASE` | Caminho do `.FDB` ASSISTENTE |
| `ASSISTENTE_FIREBIRD_USER` | Usuário |
| `ASSISTENTE_FIREBIRD_PASSWORD` | Senha |

Alternativa: seção `AssistantFirebird` em `appsettings.json` ou `appsettings.Production.local.json`.

### Publicação Conteúdos -> Assistente

Quando `PublicacaoAssistente:Enabled` estiver ativo, a API mantém scripts do banco REFERENCIAS sincronizados no banco ASSISTENTE pela rota `/scripts/publicacao`.

Configuração:

```json
{
  "PublicacaoAssistente": {
    "Enabled": true,
    "SourceKey": "conteudos-principal"
  }
}
```

| Campo | Descrição |
|-------|-----------|
| `Enabled` | Liga o worker e os endpoints de publicação |
| `SourceKey` | Identificador estável da origem gravado no ASSISTENTE; não trocar depois de publicar |

Regras de publicação:

- o padrão é mapear `pacote -> especialidades do Assistente`;
- scripts individuais podem ter regra própria `script -> especialidades`, usada no lugar do padrão do pacote;
- Laudos UX podem publicar sem MRD; Laudos Flex exigem MRD padrão;
- inativação na origem inativa também o MRD vinculado;
- exclusão local no Assistente suspende a publicação até retomada;
- vínculos locais extras do Assistente são preservados.

### Config local da API (recomendado em servidor)

Arquivo ignorado pelo git: `backend/MdwConteudos.Api/appsettings.Production.local.json`

Exemplo mínimo para produção:

```json
{
  "Urls": "http://0.0.0.0:5080",
  "WebAuth": {
    "JwtSecret": "altere-para-segredo-forte",
    "DevUserEnabled": false
  },
  "Cursor": {
    "ApiKey": ""
  }
}
```

Em produção, prefira **`CURSOR_API_KEY`** como variável de ambiente do serviço Windows/Linux em vez de gravar a chave no arquivo.

### Raiz da stack e arquivos compartilhados

A API localiza a raiz pelo conteúdo (`backend/MdwConteudos.Api` e `frontend/nuxt-app`), portanto o checkout pode ter qualquer nome. A partir dessa raiz são resolvidos `static/`, `static/uploads/`, `uploads/`, `docs/` e bancos configurados com caminho relativo.

`LegacyPaths:RepoRoot` é um override opcional para instalações fora do layout padrão ou para acesso ao repositório Flask pai. Não é necessário configurá-lo quando a estrutura acima estiver preservada.

### Chave Cursor (desenvolvimento)

```powershell
cd backend\MdwConteudos.Api
dotnet user-secrets set "Cursor:ApiKey" "crsr_sua_chave"
```

Chave em: [cursor.com/dashboard/integrations](https://cursor.com/dashboard/integrations)

### Conversor sem Cursor (testes)

Em `appsettings.json` ou `.local.json`:

```json
"Conversion": { "Provider": "Mock" }
```

---

## Subir em desenvolvimento

```powershell
# Terminal 1 — API
cd backend\MdwConteudos.Api
dotnet run

# Terminal 2 — Nuxt
cd frontend\nuxt-app
npm run dev
```

Proxy dev: `/api-dotnet` → `http://localhost:5080` ([`nuxt.config.ts`](../frontend/nuxt-app/nuxt.config.ts)).

Studio: `http://localhost:3000/studio/converter`

---

## Deploy em servidor (checklist)

### Antes do deploy

- [ ] .NET 10 runtime/SDK no servidor
- [ ] Node.js **22+** no PATH (obrigatório se usar conversor Cursor)
- [ ] Firebird acessível (REFERENCIAS + ASSISTENTE, se usar `/assistente`)
- [ ] Migrações Firebird aplicadas nos bancos usados pela API
- [ ] `.env` ou variáveis de ambiente configuradas
- [ ] `CURSOR_API_KEY` definida (se `Conversion.Provider` = `Cursor`)
- [ ] `WebAuth:DevUserEnabled` = `false` em produção
- [ ] `WebAuth:JwtSecret` com segredo forte (não usar o default de dev)
- [ ] `PublicacaoAssistente:SourceKey` definido antes de habilitar publicação

### Migrações Firebird

Execute sempre com backup prévio dos bancos.

Banco REFERENCIAS/Conteúdos:

```powershell
isql -user SYSDBA -password masterkey <CONEXAO_REFERENCIAS> -i backend\sql\publicacao-conteudos.sql
```

Banco ASSISTENTE:

```powershell
isql -user SYSDBA -password masterkey <CONEXAO_ASSISTENTE> -i backend\sql\publicacao-assistente.sql
```

Para ambientes que já receberam `publicacao-conteudos.sql` antes da criação do mapeamento por script, aplique também:

```powershell
isql -user SYSDBA -password masterkey <CONEXAO_REFERENCIAS> -i backend\sql\publicacao-conteudos-script-mapa.sql
```

Objetos esperados no REFERENCIAS:

- `ASS_PACOTE_MAPA`
- `ASS_SCRIPT_MAPA`
- `ASS_PUBLICACAO`
- triggers `ASS_*_EVENTO`

Objetos esperados no ASSISTENTE:

- `CON_PUBLICACAO`
- `CON_PUBLICACAO_ESP`
- `CON_PUBLICACAO_MRD_ESP`
- triggers/guards de publicação local

### Build e artefatos

- [ ] `dotnet publish` da API para pasta de deploy
- [ ] Copiar para o servidor, **mantendo a estrutura**:
  - `backend/agent-bridge/` (com `convert.mjs`, `package.json`, `package-lock.json`)
  - `backend/manual_scripts_html_UX.md` (manual do agente)
  - `static/` e `uploads/` conforme uso
- [ ] No servidor: `npm ci` em `backend/agent-bridge`
- [ ] `npm ci && npm run build` em `frontend/nuxt-app`
- [ ] Servir Nuxt (IIS, nginx ou `node .output/server/index.mjs`)

### Serviços sugeridos (Windows)

Ver [CUTOVER.md](CUTOVER.md): NSSM para Kestrel na porta 5080 + IIS/ARR para Nuxt e proxy.

Exemplo de regra de proxy (conceitual):

| Rota | Destino |
|------|---------|
| `/api-dotnet/*` ou `/api/*` | API .NET `:5080` |
| Demais rotas Nuxt | Nuxt `:3000` ou estático |

### Após o deploy — verificação

```powershell
# API no ar
Invoke-RestMethod http://localhost:5080/api/conversions/health

# Resposta esperada: status "healthy", provider do conversor
```

Teste funcional:

1. Abrir `/studio/converter`
2. Banner “API de conversão online”
3. Enviar imagem, escolher **HTML** ou **TXT modo texto** no modal e aguardar conversão (pode levar **até 5 minutos**; timeout configurável em `Cursor:TimeoutSeconds`)
4. TXT gerado pode ser importado em LaudosUX (`/Script/Editar` → Importar)

Teste publicação no Assistente:

1. Abrir `/scripts/publicacao`
2. Selecionar um pacote e marcar as especialidades padrão do Assistente
3. Salvar o padrão e aguardar a fila sincronizar
4. Para pacote misto, configurar regra específica em um script e validar que ele publica apenas nas especialidades selecionadas
5. Usar “Voltar ao padrão” e validar que o script herda novamente as especialidades do pacote
6. Conferir o destino em `/assistente/scripts`

Teste laudo por voz (do zero, sem imagem):

1. Abrir `/studio/voz` → **Do zero**
2. Falar a descrição completa do laudo → **Parar**
3. Agente Cursor (`intent=build`) monta o modelo; TXT modo texto é gerado automaticamente
4. Opcional: **Editar modelo com voz** para ajustes pontuais (`intent=edit`)
5. **Gerar laudo** → HTML ou TXT adicional se necessário

---

## Studio — laudo por voz

| Item | Detalhe |
|------|---------|
| UI | `/studio/voz` |
| Criar sessão | `POST /api/voice/sessions` (`mode`: `FromScratch` ou `FromImage`, imagem opcional) |
| Criar modelo (build) | `POST /api/voice/sessions/{id}/utterance` (`intent`: `build`, `transcript`) — agente Cursor gera `camposScript` |
| Editar modelo | `POST .../utterance` (`intent`: `edit`) — comandos incrementais |
| Gerar laudo | `POST /api/voice/sessions/{id}/generate` (`format`: `html` ou `modoTexto`) |
| STT servidor | `POST /api/voice/transcribe` (multipart `audio`) — requer `Voice:OpenAiApiKey` ou `OPENAI_API_KEY` |
| Provider voz | `Voice:Provider` = **`Cursor`** (padrão, agente) ou `Mock` (dev offline) |
| Bridge | `backend/agent-bridge/voice.mjs` |
| Manual | `docs/AGENTE-LAUDO-POR-VOZ.md` |
| Sessões | Em memória; TTL `Voice:SessionTtlMinutes` (padrão 120) |

Configuração em `appsettings.json`:

```json
"Voice": {
  "SessionTtlMinutes": 120,
  "Provider": "Cursor",
  "TranscriptionProvider": "Whisper",
  "VoiceBridgeScriptPath": "agent-bridge/voice.mjs",
  "WhisperModel": "whisper-1"
}
```

| Sintoma | Causa | Correção |
|---------|-------|----------|
| "Nenhuma alteração detectada" | Modo edit ou API antiga | Reiniciar API; parar gravação usa `build`; modelo vazio força build |
| Microfone não funciona | HTTP sem localhost | Usar HTTPS ou `localhost` |
| Transcrição servidor falha | Sem chave OpenAI | `OPENAI_API_KEY` ou `Voice:OpenAiApiKey` |
| Sessão expirada | TTL ou restart da API | Iniciar nova sessão e exportar cedo |

---

## Studio — conversor imagem (requisitos específicos)

| Item | Detalhe |
|------|---------|
| Endpoint | `POST /api/conversions` (multipart: `image`, opcional `format` = `html` ou `modoTexto`) |
| Health | `GET /api/conversions/health` (lista formatos suportados) |
| Provider padrão | `Cursor` → `CursorComposerImageToHtmlConverter` |
| Bridge | `backend/agent-bridge/convert.mjs` (4º arg: `html` ou `modoTexto`) |
| Dependência npm | `@cursor/sdk` (Node >= 22) |
| Timeout | `Cursor:TimeoutSeconds` (padrão 300) |
| Manual HTML LaudosUX | `backend/manual_scripts_html_UX.md` |
| Manual TXT modo texto | `backend/AGENTE-MODELOS-MODO-TEXTO.md` |

Erros comuns:

| Sintoma | Causa | Correção |
|---------|-------|----------|
| `Cursor API key não configurada` | Sem chave | `CURSOR_API_KEY` ou User Secrets |
| `Cannot find package '@cursor/sdk'` | Bridge sem deps | `npm install` em `backend/agent-bridge` |
| `Bridge Node não encontrado` | Caminho errado após publish | Manter `agent-bridge/` relativo à API ou ajustar `Cursor:BridgeScriptPath` |
| API online, conversão falha | Node fora do PATH | Instalar Node 22+ e reiniciar serviço da API |

---

## Variáveis opcionais (e-mail, URLs)

Usadas por alguns fluxos legados/migrados ([`ScriptsService.cs`](../backend/MdwConteudos.Api/Modules/Web/ScriptsService.cs)):

- `BASE_URL`
- `SMTP_SERVER`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_SENDER`

---

## Referências

- [README.md](../README.md) — visão geral e dev rápido
- [CUTOVER.md](CUTOVER.md) — cutover Flask → Nuxt + proxy IIS
- [azure-pipelines-migracao.example.yml](azure-pipelines-migracao.example.yml) — rascunho CI (atualizar Node para 22.x se incluir Studio)
- [manual_scripts_html_UX.md](../backend/manual_scripts_html_UX.md) — regras HTML LaudosUX para o agente
# Produção IIS: procedimento vigente

Para a nova VM Azure Windows, seguir [PRODUCAO_IIS.md](PRODUCAO_IIS.md) e `tools/production`. Este procedimento substitui as instruções históricas abaixo de NSSM, Kestrel público e cópia manual. O conteúdo abaixo é referência de desenvolvimento/integrações, não configuração aprovada de produção.
