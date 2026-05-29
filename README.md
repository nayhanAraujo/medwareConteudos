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

## Endpoints principais

| Área | Prefixo |
|------|---------|
| API parceiros (JWT) | `/apiconteudos/v1` |
| API interna (sem JWT) | `/api/v1` |
| Web (Nuxt + JWT usuário) | `/api/web` |
| Swagger | `/swagger` |

## Estrutura

```
mdw-migracao/
├── backend/MdwConteudos.Api/   # ASP.NET Core 8 + Dapper + Firebird
├── frontend/nuxt-app/          # Nuxt 3 + Pinia + Bootstrap + SweetAlert2
├── docs/CUTOVER.md             # Guia de cutover e pipeline
└── README.md
```

## Módulos migrados (resumo)

- Auth / usuários (SHA-256, JWT web)
- API pública (`routes/api.py`) — paridade de rotas
- Web: variáveis, scripts, relatórios, referências, impressos, conteúdos, painéis, agente (listagens)
- Automação E2E: **fora do escopo** (descontinuada na nova stack)

## Próximos passos (cutover)

Ver [docs/CUTOVER.md](docs/CUTOVER.md).
