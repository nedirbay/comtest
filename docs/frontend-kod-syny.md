# Frontend kod syny çek-listi

Şu kadalaryň köpüsini ESLint, TypeScript we brauzer testleri eýýäm barlaýar. Bu sanaw olaryň tutup bilmeýän zatlary üçin.

## XSS

- Serwerden gelen HTML diňe `SafeHtml` arkaly görkezilýär. `v-html` başga ýerde ulanylmaýar.
- Daşyndan gelen salgy (`href`, `src`) `safeUrl`-dan geçýär: Vue teksti goraýar, ýöne `javascript:` salgysyny däl.
- Dükanyň teksti `<script>` içine düşse (JSON-LD), `useJsonLd` ulanylýar, ol `</script>` bilen çykmagyň öňüni alýar.
- Täze `<style>` ýa-da inline skript CSP-a laýyk gelýärmi? Brauzer testleri CSP-ni ýerine ýetirýän re­žimde işleýär.

## CSRF we sessiýa

- API-ä soraglar diňe `@comtest/api-client` arkaly: CSRF sözbaşysy we token şol ýerde goşulýar.
- Token hiç haçan `localStorage`-a, `sessionStorage`-a, URL-e ýa-da loga ýazylmaýar.
- Ulgamdan çykanda arassalanmaly täze maglumat bar bolsa, `auth.onSignedOut`-a goşulýar.
- `returnUrl` ýaly salgy parametrleri `safeReturnUrl`-dan geçýär.

## Açyk maglumat

- Serwerde render edilýän sahypalarda ulanyja degişli maglumat ýok: şahsy sahypalar `routeRules`-da `ssr: false`.
- `VITE_*` we `NUXT_PUBLIC_*` üýtgeýjilerinde gizlin maglumat ýok.
- Ýalňyşlyk habarlarynda we Sentry hasabatlarynda şahsy maglumat ýok.
- Müşderiniň maglumatlary permission bilen gizlenýär, ýöne hakyky barlagyň serwerdedigi ýatdan çykarylmaýar.

## Pul we sargyt

- Brauzerde pul hasaplanmaýar: jemi serwer jogabyndan alynýar.
- Sargyt ýa-da töleg döredýän düwme iki gezek basylanda iki amal döretmeýär (`Idempotency-Key`).
- Üstünlik sahypasy URL-e däl, serwer jogabyna ynanýar.

## Täze paket

- Paket hakykatdan gerekmi, ýa-da 10 setir kod ýeterlikmi?
- Install skripti barmy? `allowBuilds`-a diňe sebäbi düşündirilse goşulýar.
- Brauzer bundle-yna düşýärmi? `pnpm size` býujetini barlaň.
