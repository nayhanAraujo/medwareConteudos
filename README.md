# MDW Conteúdos — Stack nova (migração)

Pasta **gitignored** com Nuxt 3 + ASP.NET Core 8. O Flask/Jinja na raiz do repositório continua funcionando até o cutover módulo a módulo.

## Pré-requisitos

- .NET 8 SDK
- Node.js 18+
- Firebird (mesmo `BD/REFERENCIAS.FDB` e `.env` do projeto pai)

## Subir em desenvolvimento

### 1. API .NET (porta 5080)

```powershell
cd mdw-migracao\backend\MdwConteudos.Api
dotnet run
```

A API lê o `.env` da raiz do repositório (`FIREBIRD_*`, `SECRET_KEY`, `API_JWT_*`).

### 2. Nuxt (porta 3000)

```powershell
cd mdw-migracao\frontend\nuxt-app
npm run dev
```

Proxy: `/api-dotnet` → `http://localhost:5080`

### 3. Flask legado (opcional, porta 5000)

```powershell
cd ..\..
.\venv\Scripts\python.exe app.py
```

### 4. Studio — Conversor HTML (unificado)

Rotas Nuxt em `/studio` (mesmo app na porta **3000**). API em `/api/conversions` na porta **5080**.  
O card **Studio** em `/conteudos` abre `http://localhost:3000/studio` em nova aba.

Bridge do agente Cursor (conversão real):

```powershell
cd backend\agent-bridge
npm install

cd ..\MdwConteudos.Api
dotnet user-secrets set "Cursor:ApiKey" "crsr_sua_chave"
```

Alternativa: variável `CURSOR_API_KEY`. Para testar sem Cursor, em `appsettings.json`: `"Conversion": { "Provider": "Mock" }`.

Manual do agente: [`backend/manual_scripts_html_UX.md`](backend/manual_scripts_html_UX.md).

## Endpoints principais

| Área | Prefixo |
|------|---------|
| API parceiros (JWT) | `/apiconteudos/v1` |
| API interna (sem JWT) | `/api/v1` |
| Conversor HTML (Studio) | `/api/conversions` |
| Web (Nuxt + JWT usuário) | `/api/web` |
| Swagger | `/swagger` |

## Estrutura

```
mdw-migracao/
├── backend/
│   ├── MdwConteudos.Api/          # ASP.NET Core 8 (inclui /api/conversions)
│   ├── ConversorHtml.Application/ # Conversão imagem → HTML
│   ├── ConversorHtml.Domain/
│   ├── agent-bridge/              # Cursor Composer bridge
│   └── manual_scripts_html_UX.md
├── frontend/nuxt-app/             # Nuxt + hub + /studio (Conversor)
├── docs/CUTOVER.md
└── README.md
```

## Módulos migrados (resumo)

- Auth / usuários (SHA-256, JWT web)
- API pública (`routes/api.py`) — paridade de rotas
- Web: variáveis, scripts, relatórios, referências, impressos, conteúdos, painéis, agente (listagens)
- Automação E2E: **fora do escopo** (descontinuada na nova stack)

## Próximos passos (cutover)

Ver [docs/CUTOVER.md](docs/CUTOVER.md).
