# MDW Conteúdos — Stack nova (migração)

Stack de migração com Nuxt 4 + ASP.NET Core 10. O Flask/Jinja no repositório legado pai continua funcionando até o cutover módulo a módulo.

Ficam deliberadamente no legado: `uploads/uploaddll`, automação E2E e os módulos de IA (Agente PubMed/Grok, Oráculo/XML e conversor Azure OpenAI). Essa exclusão não abrange importação de variáveis `.cs`, impressos `.mrd`, anexos ou arquivos de versões.

## Pré-requisitos

**Portal da API:** `/apiconteudos/docs`. Configuração, acesso e proxy em [docs/API_PORTAL.md](docs/API_PORTAL.md); preparação segura de cópias em [docs/API_HOMOLOGACAO.md](docs/API_HOMOLOGACAO.md).

- .NET 10 SDK
- Node.js 18+ (frontend Nuxt); **Node.js 22+** obrigatório para o conversor Studio (`backend/agent-bridge`)
- Firebird (bancos configurados em `bd/` ou por variáveis de ambiente)

**Deploy em servidor:** checklist completo em [docs/DEPLOY.md](docs/DEPLOY.md) (pacotes npm, variáveis de ambiente, Studio/Cursor, verificação pós-deploy).

## Subir em desenvolvimento

### 1. API .NET (porta 5080)

```powershell
cd backend\MdwConteudos.Api
dotnet run
```

A API lê o `.env` da raiz detectada da stack (`FIREBIRD_*`, `SECRET_KEY`, `API_JWT_*`).

Para restaurar, compilar ou testar todo o backend de uma vez, use a solução:

```powershell
dotnet restore backend\MdwConteudos.slnx
dotnet build backend\MdwConteudos.slnx
dotnet test backend\MdwConteudos.slnx
```

### 2. Nuxt (porta 3000)

```powershell
cd frontend\nuxt-app
npm run dev
```

Proxy: `/api-dotnet` → `http://localhost:5080`

### 3. Flask legado (opcional, porta 5000)

```powershell
cd ..\..
.\venv\Scripts\python.exe app.py
```

### 4. Studio — Conversor imagem (HTML ou TXT modo texto)

Rotas Nuxt em `/studio` (mesmo app na porta **3000**). API em `/api/conversions` na porta **5080**.  
O card **Studio** em `/conteudos` abre `http://localhost:3000/studio` em nova aba.

Após clicar em **Converter**, escolha **HTML LaudosUX** ou **TXT modo texto** (importável em LaudosUX → Script → Importar, `tipoScript = 3`).

Bridge do agente Cursor (conversão real):

```powershell
cd backend\agent-bridge
npm install

cd ..\MdwConteudos.Api
dotnet user-secrets set "Cursor:ApiKey" "crsr_sua_chave"
```

Alternativa: variável `CURSOR_API_KEY`. Para testar sem Cursor, em `appsettings.json`: `"Conversion": { "Provider": "Mock" }`.

### 5. Studio — Laudo por voz

Rota Nuxt: `/studio/voz`. API: `/api/voice/sessions` (sessão em memória, TTL configurável).

- **Do zero:** fale a descrição completa → agente Cursor gera o modelo (`camposScript`) e TXT modo texto ao parar.
- **Com imagem:** bootstrap inicial a partir do layout; a voz edita (adicionar/remover/mover campos).
- **STT:** Web Speech API no browser (padrão) ou upload de áudio com Whisper (`POST /api/voice/transcribe`).
- **Provider padrão:** `Voice:Provider: Cursor` (agente via `voice.mjs`). Mock apenas para dev offline.

Dev offline sem Cursor: `"Voice": { "Provider": "Mock" }` em `appsettings.json`.  
Manual do agente: [`docs/AGENTE-LAUDO-POR-VOZ.md`](docs/AGENTE-LAUDO-POR-VOZ.md).  
Microfone exige **HTTPS** ou **localhost**.

Manual do agente HTML: [`backend/manual_scripts_html_UX.md`](backend/manual_scripts_html_UX.md).  
Manual modo texto: [`backend/AGENTE-MODELOS-MODO-TEXTO.md`](backend/AGENTE-MODELOS-MODO-TEXTO.md).  
Instalação completa, produção e troubleshooting: [`docs/DEPLOY.md`](docs/DEPLOY.md).

### 6. Publicação no Assistente

Rota Nuxt: `/scripts/publicacao`. API: `/api/web/assistente/publicacao`.

O módulo publica scripts do banco **REFERENCIAS/Conteúdos** no banco **ASSISTENTE**, mantendo os modelos do Assistente sincronizados a partir da origem. A associação principal é `pacote -> especialidades`; quando um pacote mistura especialidades, a tela permite definir uma regra específica `script -> especialidades`, que substitui o padrão do pacote apenas para aquele script.

Regras principais:

- scripts Laudos UX podem ser publicados sem MRD;
- scripts Laudos Flex exigem MRD padrão;
- ao inativar a origem, o script e o MRD vinculado são inativados no Assistente;
- exclusão local no Assistente suspende a publicação até retomada;
- edição local de título, tipo e estrutura de scripts sincronizados é bloqueada.

Configuração:

```json
"PublicacaoAssistente": {
  "Enabled": true,
  "SourceKey": "conteudos-principal"
}
```

Migrações Firebird:

- `backend/sql/publicacao-conteudos.sql` no banco REFERENCIAS novo;
- `backend/sql/publicacao-assistente.sql` no banco ASSISTENTE;
- `backend/sql/publicacao-conteudos-script-mapa.sql` em bancos REFERENCIAS que já tinham recebido a publicação inicial antes do mapeamento por script.

## Endpoints principais

| Área | Prefixo |
|------|---------|
| API parceiros (JWT) | `/apiconteudos/v1` |
| API interna (sem JWT) | `/api/v1` |
| Conversor Studio (HTML / TXT) | `/api/conversions` |
| Laudo por voz (Studio) | `/api/voice/sessions` |
| Publicação no Assistente | `/api/web/assistente/publicacao` |
| Web (Nuxt + JWT usuário) | `/api/web` |
| Swagger | `/swagger` |

## Estrutura

```
<raiz-do-projeto>/
├── backend/
│   ├── MdwConteudos.slnx          # Solução .NET completa do backend
│   ├── MdwConteudos.Api/          # ASP.NET Core 10 (inclui /api/conversions)
│   ├── ConversorHtml.Application/ # Conversão imagem → HTML ou TXT
│   ├── ConversorHtml.Domain/
│   ├── agent-bridge/              # Cursor Composer bridge (convert.mjs, voice.mjs)
│   ├── sql/                       # Migrações Firebird da stack nova
│   └── AGENTE-LAUDO-POR-VOZ.md
├── frontend/nuxt-app/             # Nuxt + hub + /studio (Conversor)
├── docs/CUTOVER.md
├── docs/DEPLOY.md                 # Requisitos de runtime e deploy (servidor)
├── docs/PLANO_REFATORACAO_ESTRUTURA.md  # Plano incremental (alta prioridade)
└── README.md
```

## Módulos migrados (resumo)

- Auth / usuários (SHA-256, JWT web)
- API pública (`routes/api.py`) — paridade de rotas
- Web: variáveis, fórmulas, modelos, scripts, relatórios, referências, impressos, conteúdos, painéis, usuários e cadastros-base
- Assistente: cadastros, importação/exportação de modelos, vínculos, agrupamento por especialidade e publicação sincronizada a partir dos pacotes/scripts do Conteúdos
- IA legada (Agente PubMed/Grok e Oráculo/XML): **fora do escopo**
- Conversor Azure OpenAI: **fora do escopo**
- Upload/importação legada `uploaddll`: **fora do escopo**
- Automação E2E: **fora do escopo** (descontinuada na nova stack)

## Próximos passos (cutover)

Ver [docs/CUTOVER.md](docs/CUTOVER.md).

Refatoração de estrutura (paths, git, Modules/Web): [docs/PLANO_REFATORACAO_ESTRUTURA.md](docs/PLANO_REFATORACAO_ESTRUTURA.md).
