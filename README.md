# ComTest

Dükanlar we söwda firmalary üçin multi-tenant e-commerce platforma.

| Bukja | Mazmuny | Resminama |
|---|---|---|
| [`backend/`](backend/) | ASP.NET Core API (.NET 10), PostgreSQL | [backend/README.md](backend/README.md) |
| [`frontend/`](frontend/) | Storefront (Nuxt), admin panel (Vue + Vite) | [frontend/README.md](frontend/README.md) |
| [`deploy/`](deploy/) | Ösdürmek üçin ýerli hyzmatlar (Docker Compose) | |
| [`docs/`](docs/) | Arhitektura kararlary (ADR) | [docs/adr/](docs/adr/) |
| [`task.md`](task.md) | Iş meýilnamasy we ýerine ýetirilen işler | |

## Işe girizmek

```bash
# 1. API: ilkinji sazlamalar üçin backend/README.md-ä serediň
cd backend && dotnet run --project src/ComTest.Api   # http://localhost:5050

# 2. Frontend
cd frontend
nvm use && corepack enable && pnpm install
pnpm dev   # storefront: http://localhost:3000, admin: http://localhost:5173
```
