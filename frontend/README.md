# ComTest frontend

pnpm monorepo: Vue 3, TypeScript, Element Plus, Tailwind CSS.

| Paket      | Ýoly                  | Wezipesi                                                         |
| ---------- | --------------------- | ---------------------------------------------------------------- |
| Storefront | `apps/storefront`     | Müşderi sahypalary, Nuxt SSR                                     |
| Admin      | `apps/admin`          | Dolandyryş paneli, Vite SPA                                      |
| UI         | `packages/ui`         | Dizaýn tokenleri, umumy elementler, formatlaýjylar, dil faýllary |
| Auth       | `packages/auth`       | Giriş formalary, sessiýa store-y, `v-can`                        |
| API client | `packages/api-client` | OpenAPI-den döredilen tipler, HTTP gatlagy, token dolandyryşy    |
| Config     | `packages/config`     | TypeScript, ESLint, Vite sazlamalary                             |

Kararlaryň sebäpleri: [docs/adr/0001-frontend-gurlusy.md](../docs/adr/0001-frontend-gurlusy.md).

## Işe girizmek

Node `>=24.15` gerek (`nvm use`). Backend işläp durmaly.

```bash
corepack enable
pnpm install
cp apps/storefront/.env.example apps/storefront/.env

pnpm dev               # ikisi bilelikde
pnpm dev:storefront    # http://localhost:3000
pnpm dev:admin         # http://localhost:5173
```

Development-de storefront dükany `NUXT_PUBLIC_DEV_TENANT` boýunça saýlaýar. Demo dükan üçin backend-de `dotnet run --project src/ComTest.Api -- seed` işlediň.

## Buýruklar

```bash
pnpm test              # unit testler (Vitest)
e2e/stack.sh up        # brauzer testleri üçin aýratyn backend, baza we poçta (Docker gerek)
pnpm test:e2e          # brauzer testleri, ilki stack işe girizilen bolmaly
e2e/stack.sh down      # synag gurşawyny we onuň maglumatlaryny poz
pnpm test:visual       # ekran suratlaryny deňeşdirmek (täzelemek: test:visual:update)
pnpm test:lighthouse   # Lighthouse, storefront 3001-nji portda işläp durmaly
pnpm typecheck
pnpm lint
pnpm format
pnpm build
pnpm api:sync          # API kontraktyny işläp duran backend-den täzele
```

Commit-ler [Conventional Commits](https://www.conventionalcommits.org) görnüşinde bolmaly: `feat(admin): haryt sanawy`. Muny `commit-msg` hook-y barlaýar.

## Kadalar

- **Serwer maglumatlary** TanStack Query-de, **müşderi ýagdaýy** Pinia-da saklanýar.
- **Pul hasaplanmaýar.** Ähli jemi API-den gelýär, frontend diňe görkezýär.
- **Tekstler** `i18n` faýllarynda üç dilde. Dilleriň biri ýetmese, TypeScript ýalňyşlyk berýär.
- **Reňkler** tokenlerden (`bg-surface`, `text-ink`, `text-primary`). `bg-white` ýaly gönüden-göni bahalar gara temany bozýar.
- **`v-html` gadagan** (ESLint). Kadadan çykma diňe DOMPurify bilen we düşündiriş bilen.
- **`localStorage`/`sessionStorage` gadagan** (ESLint). Zyýansyz interfeýs saýlawlary üçin `usePreference` bar. Token we şahsy maglumat brauzerde saklanmaýar.
- **`VITE_*` we `NUXT_PUBLIC_*`** brauzerde görünýär: gizlin maglumat ýazmaň.
- **Daşyndan gelen salgy** `safeUrl`-dan, **serwerden gelen HTML** `SafeHtml`-dan geçýär.
- **`zod`** göni däl-de, `@comtest/ui/zod` arkaly import edilýär (CSP).
- **API ýagdaýlary** OpenAPI-den gelýän tipler bilen deňeşdirilýär we `StatusTag` bilen görkezilýär.
- **Täze paket** azyndan bir hepde öň çap edilen bolmaly (`minimumReleaseAge`), install skriptleri bolsa `allowBuilds` sanawy boýunça.

Kod syny üçin: [docs/frontend-kod-syny.md](../docs/frontend-kod-syny.md).

## Production sazlamalary

Storefront: `NUXT_PUBLIC_API_URL` (boş bolsa, şol bir domen), `NUXT_API_INTERNAL_URL`, `NUXT_PUBLIC_ADMIN_URL`, `NUXT_CSP_MODE` (`report` ýa-da `enforce`). `NUXT_PUBLIC_DEV_TENANT` production-da boş galmaly: dükan host adyndan kesgitlenýär.

Admin (build wagtynda): `VITE_API_URL`, `VITE_CSP_EXTRA_ORIGINS` (surat ýükleýän saklaýyşyň salgysy, ýogsam ýüklemek petiklenýär). Web serwer `Content-Security-Policy: frame-ancestors 'none'` sözbaşysyny goşmaly.
