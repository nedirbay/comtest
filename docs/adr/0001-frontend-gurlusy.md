# ADR 0001: Frontend gurluşy

**Ýagdaýy:** kabul edildi, 2026-11-03

## Mesele

Müşderi sahypalary gözleg ulgamlarynda görünmeli, admin panele bolsa bu gerek däl. Şol bir wagtda iki programma bir dizaýny, bir API müşderisini we bir giriş akymyny paýlaşmaly. `task.md` K6 we K7 kararlaryny kesgitleýär, ýöne olaryň jikme-jiklikleri işiň dowamynda anyklandy.

## Kararlar

**Storefront Nuxt (SSR), admin Vite SPA.** Şahsy sahypalar (`/account/**`) SSR-den çykarylýar, şonuň üçin ulanyjy maglumaty serwerde render edilmeýär we SSR payload-yna düşmeýär.

**Dördünji umumy paket: `packages/auth`.** `task.md` üç paket görkezýär (`ui`, `api-client`, `config`). Giriş, 2FA we sessiýa formalary iki programmada hem birmeňzeş. Olary `ui`-a goýsak, dizaýn paketi API-ä bagly bolardy, her programmada aýratyn ýazsak, howpsuzlyk logikasy iki ýerde durardy.

**Paketler çeşme kody görnüşinde ulanylýar.** Aýratyn build ýok: programmalar `.vue` we `.ts` faýllaryny özleri compile edýär. Şeýlelikde Element Plus-yň zerur bolanda ýüklenmegi paketlerdäki elementlerde hem işleýär.

**TypeScript 5.9, täze 7.x däl.** `typescript-eslint` `<6.1`, `openapi-typescript` `^5` talap edýär. Bu paketler täzelenende wersiýa täzeden gözden geçirilýär.

**CSS `@layer` tertibi:** `theme, base, element-plus, element-bridge, components, utilities`. Element Plus-yň elementler boýunça ýüklenýän CSS-i ýörite Vite plugin-i bilen layer-e salynýar (`packages/config/vite.js`). Ýogsam layer-siz CSS ähli layer-lerden güýçli bolar we Tailwind utility-leri elementleri üýtgedip bilmezdi.

**Access token diňe `AuthSession` obýektiniň hususy meýdanynda.** Sahypa täzelenende refresh cookie arkaly dikeldilýär. `localStorage`-da diňe „bu brauzerde sessiýa bolmagy mümkin“ diýen bellik bar: ol kimlik maglumaty däl, myhmanlary her sahypada şowsuz refresh soragyndan gutarýar.

**Sana we pul formatlary `Intl`-yň `tk` maglumatlaryna daýanmaýar.** Brauzerleriň we Node-yň köpüsinde `tk` üçin maglumat ýok ýa-da dürli, bu bolsa SSR hydration ýalňyşlygyna getirýär.

## Netijeler

- API kontrakty üýtgände `pnpm api:sync` işledilýär we `openapi/v1.json` üýtgeşmesi kod synynda görünýär.
- `@tanstack/vue-query` client-i Nuxt-da her sorag üçin täzeden döredilýär, şonuň üçin ony modul derejesinde saklamak bolmaýar.
