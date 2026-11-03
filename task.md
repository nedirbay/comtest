# E-commerce platforma — iş meýilnamasy

Dükanlar we söwda firmalary üçin döwrebap e-commerce platforma.

- **Backend:** ASP.NET Core (.NET 10)
- **Frontend:** Vue 3 + TypeScript, Element Plus, Tailwind CSS

## Bu faýly nädip ulanmaly

- `- [ ]` — edilmeli iş, `- [x]` — ýerine ýetirilen iş.
- 🔒 — howpsuzlyk işi. Bu işler soňa goýulmaýar: her biri öz tapgyrynda ýerine ýetirilýär.
- `[MVP]`, `[V1]`, `[V2]` — mümkinçiligiň haýsy wersiýa degişlidigi.
- `✅ backend` — mümkinçiligiň API bölegi taýýar, interfeýsi entek ýok. Guty diňe mümkinçilik ulanyja tutuşlygyna elýeterli bolanda belgilenýär. „✅ backend: X; Y ýok“ ýazgysy diňe X-iň taýýardygyny aňladýar.
- Tapgyr diňe onuň ähli işleri, testleri we 🔒 işleri gutarandan soň tamamlanan hasaplanýar.

## Ýol kartasy

| Etap | Netije | Umumy | Backend | Frontend |
|---|---|---|---|---|
| **M0 — Esas** | Taslama işleýär, ulanyjy hasaba alnyp, ulgama girip bilýär | U0 | B0–B3 | F0–F3 |
| **M1 — MVP** | Satuw doly işleýär: katalog, sebet, sargyt, nagt töleg, admin panel | P1–P2 | B4–B9, B10 (nagt), B11 | F4–F9 |
| **M2 — V1** | Onlaýn töleg, arzanladyşlar, bildirişler, gözleg, hasabatlar | P3 | B10 (onlaýn), B12–B17 | F10–F12 |
| **M3 — Production ready** | Howpsuzlyk auditi, ýük synagy, gözegçilik, ätiýaçlyk nusgalar | P4–P8 | B18–B20 | F13–F15 |
| **M4 — V2** | Marketplace, POS, mobil programma, AI mümkinçilikleri | P9 | `[V2]` bellikli işler | `[V2]` bellikli işler |

## Esasy kararlar

Bu kararlar meýilnamanyň esasyny düzýär. Işe başlamazdan öň tassyklamaly. `[x]` bellik kararyň kodda ulanylýandygyny aňladýar.

- [x] **K1. Platforma modeli:** multi-tenant SaaS — bir platformada köp firma, her firmanyň öz dükany bar. Bir firma üçin hem şol bir kod işleýär.
- [x] **K2. Arhitektura:** modular monolith + Clean Architecture. Microservice başda artykmaç çylşyrymlylyk döredýär; gerek bolanda modullar aýrylyp bilner.
- [x] **K3. Maglumatlar bazasy:** PostgreSQL + Row-Level Security (tenant üzňeligi baza derejesinde hem goralýar).
- [ ] **K4. Keş we fon işleri:** Redis, Hangfire; gerek bolanda RabbitMQ.
- [x] **K5. Faýl saklaýyş:** S3-e laýyk saklaýyş (dev: MinIO) + CDN. (CDN üçin sazlama bar; CDN-iň özi P3-de goşulýar.)
- [x] **K6. Storefront:** Nuxt (SSR, Vue 3 + TS). Haryt sahypalary gözleg ulgamlarynda görünmeli, arassa SPA munuň üçin ýaramaz.
- [x] **K7. Admin panel:** Vue 3 + Vite SPA (SEO gerek däl).
- [x] **K8. Auth:** gysga ömürli access JWT (brauzeriň RAM-ynda) + refresh token (`HttpOnly` cookie, rotation).
- [x] **K9. Diller:** türkmen, rus, iňlis. (Interfeýs terjimeleri her sahypa bilen bilelikde goşulýar.)
- [x] **K10. Pul:** esasy pul birligi TMT, goşmaça USD. Pul mukdarlary `decimal` görnüşinde saklanýar, `float`/`double` ulanylmaýar.
- [x] **K11. Töleg:** provider abstraksiýasy. Tertip: alanda nagt → bank geçirimi → ýerli bank ekwaýringi → halkara provider. Kart maglumatlary biziň serwerimize düşmeýär. (Abstraksiýa, nagt we bank geçirimi taýýar; ekwaýring B10-da.)
- [x] **K12. Kitaphana ygtyýarnamalary:** MediatR, AutoMapper, MassTransit (v9+) we FluentAssertions (v8+) täjirçilik ygtyýarnamasyna geçdi. Olaryň ýerine öz handler-lerimiz, Mapperly, AwesomeAssertions/Shouldly ulanylýar. (Mapperly we AwesomeAssertions/Shouldly gerek bolmady: mapping el bilen, testlerde xUnit `Assert`.)

---

# 1-nji bölüm. Mümkinçilikler sanawy

## 1.1. Müşderi tarapy (storefront)

**Katalog we gözleg**

- [x] `[MVP]` Kategoriýalar agajy we haryt sanawy
- [x] `[MVP]` Haryt sahypasy: suratlar galereýasy, düşündiriş, häsiýetnamalar, galyndy ýagdaýy
- [x] `[MVP]` Haryt wariantlary (reňk, ölçeg, göwrüm)
- [x] `[MVP]` Tekst boýunça gözleg (ýönekeý `LIKE`; FTS B15-de)
- [ ] `[MVP]` Süzgüçler (baha, marka, häsiýet) we tertipleşdirme — ✅ baha, tertipleşdirme; marka diňe haryt sahypasyndan (açyk marka sanawy API-de ýok); häsiýet boýunça süzgüç ýok
- [ ] `[V1]` Gözlegde awtomatik doldurma we ýalňyş ýazuwa çydamlylyk — ✅ awtomatik doldurma; ýalňyş ýazuwa çydamlylyk ýok (B15)
- [x] `[V1]` Harytlary deňeşdirmek
- [ ] `[V1]` Soňky görlen harytlar, meňzeş harytlar, „bilelikde satyn alynýar“ — ✅ soňky görlenler, meňzeş harytlar (şol bir kategoriýa boýunça); „bilelikde satyn alynýar“ ýok
- [ ] `[V1]` Haryt peýda bolanda habar bermek
- [ ] `[V2]` AI esasly şahsy teklipler
- [ ] `[V2]` Surat boýunça gözleg

**Sebet we sargyt**

- [x] `[MVP]` Sebet (myhman we hasaba alnan ulanyjy üçin, ulgama girende birleşýär)
- [x] `[MVP]` Myhman hökmünde sargyt etmek
- [x] `[MVP]` Sargyt resmileşdirmek: salgy, eltip bermek görnüşi, töleg görnüşi
- [x] `[MVP]` Eltip bermegiň bahasynyň hasaplanmagy
- [x] `[MVP]` Sargyt ýagdaýyny yzarlamak
- [ ] `[V1]` Promokod we sowgat kartasyny ulanmak
- [ ] `[V1]` Taşlanan sebet barada ýatlatma
- [ ] `[V1]` Bir basmada gaýtadan sargyt etmek
- [ ] `[V2]` Bölüp tölemek (nesiýe, kredit)
- [ ] `[V2]` Yzygiderli sargyt (abuna)

**Şahsy otag**

- [x] `[MVP]` Hasaba alynmak, ulgama girmek (e-poçta/telefon) (hasaba alyş e-poçta bilen, telefon soň tassyklanýar; girmek ikisi bilen hem)
- [ ] `[MVP]` Profil we salgylar kitaby — ✅ salgylar, profili görmek; profili üýtgetmek üçin API ýok
- [x] `[MVP]` Sargytlaryň taryhy
- [ ] `[V1]` Halanlarym sanawy
- [ ] `[V1]` Haryt barada syn we baha (suratly)
- [ ] `[V1]` Yzyna gaýtarma haýyşy
- [ ] `[V1]` Bonus ballary we wepalylyk derejesi
- [ ] `[V1]` Iki basgançakly tassyklama (2FA), Google bilen girmek — ✅ 2FA (backend we interfeýs); Google ýok
- [ ] `[V1]` Şahsy maglumatlary göçürip almak we hasaby pozmak

**Umumy**

- [x] `[MVP]` Köp dilli interfeýs (tk, ru, en)
- [x] `[MVP]` Responsiw dizaýn (telefon, planşet, kompýuter)
- [ ] `[V1]` Köp pul birligi
- [x] `[V1]` Gara tema
- [ ] `[V1]` PWA (telefona gurnamak, push bildirişler) — ✅ gurnamak, offline sahypa; push üçin backend ýok
- [ ] `[V1]` Onlaýn kömek söhbetdeşligi
- [ ] `[V2]` Android/iOS programma
- [ ] `[V2]` Telegram bot arkaly sargyt

## 1.2. Dükan eýesi tarapy (admin panel)

**Harytlar we ammar**

- [x] `[MVP]` Harytlary, kategoriýalary, markalary dolandyrmak
- [x] `[MVP]` Wariantlar, SKU, ştrih-kod, suratlar
- [x] `[MVP]` Ammar galyndysyny dolandyrmak, az galanda duýduryş
- [ ] `[V1]` Excel/CSV arkaly köpçülikleýin import/eksport
- [ ] `[V1]` Köp ammar we şahamça
- [ ] `[V1]` Ammar hereketleriniň taryhy, inwentarizasiýa — ✅ hereketleriň taryhy (backend we interfeýs); inwentarizasiýa ýok
- [ ] `[V1]` Üpjünçiler we satyn alma sargytlary
- [ ] `[V2]` 1C/ERP bilen sinhronlaşdyrmak

**Sargytlar we müşderiler**

- [x] `[MVP]` Sargytlary görmek, süzmek, ýagdaýyny üýtgetmek
- [x] `[MVP]` Müşderiler sanawy we sargyt taryhy
- [ ] `[V1]` Faktura we goşma haty çap etmek (PDF)
- [ ] `[V1]` Yzyna gaýtarma we pul gaýtarma — ✅ pul gaýtarma (backend we interfeýs); yzyna gaýtarma (RMA) ýok
- [ ] `[V1]` Kurýerlere sargyt bellemek
- [ ] `[V1]` Müşderi toparlary we CRM bellikleri
- [ ] `[V2]` POS (dükandaky kassa) onlaýn ammar bilen bilelikde

**Marketing**

- [ ] `[V1]` Arzanladyşlar, promokodlar, gysga möhletli aksiýalar
- [ ] `[V1]` Bannerler we baş sahypany dolandyrmak
- [ ] `[V1]` CMS sahypalary we blog
- [ ] `[V1]` SEO sazlamalary (meta, slug, sitemap) — ✅ meta, slug, sitemap harytlar we kategoriýalar üçin; sahypa derejesindäki sazlama ýok
- [ ] `[V1]` E-poçta/SMS kampaniýalary
- [ ] `[V2]` Referal programmasy
- [ ] `[V2]` AI bilen haryt düşündirişini taýýarlamak

**Dolandyryş we hasabatlar**

- [ ] `[MVP]` Dolandyryş paneli (dashboard): satuw, sargyt, galyndy — ✅ sargyt we galyndy görkezijileri, grafik; satuw mukdary üçin API ýok (B16)
- [ ] `[MVP]` Dükan sazlamalary: logotip, aragatnaşyk, eltip bermek, töleg — ✅ umumy, logotip, eltip bermek, dil; töleg sazlamalaryny ýazmak bar, okamak üçin API ýok
- [x] `[MVP]` Işgärler we rollar
- [ ] `[V1]` Satuw, haryt, müşderi hasabatlary we Excel eksporty
- [ ] `[V1]` Audit žurnaly (kim, haçan, näme üýtgetdi)
- [ ] `[V1]` Salgyt sazlamalary
- [ ] `[V1]` Dükanyň öz domeni we tema sazlamalary
- [ ] `[V2]` Girdeji we gazanç seljermesi, isleg çaklamasy

## 1.3. Platforma tarapy (super admin)

- [ ] `[V1]` Firmalary hasaba almak, tassyklamak, wagtlaýyn togtatmak — ✅ togtatmak (backend we interfeýs), hasaba almak (backend); tassyklamak ýok
- [ ] `[V1]` Tarif meýilnamalary we çäklendirmeler
- [ ] `[V1]` Abuna tölegleri we hasaplar
- [ ] `[V1]` Platforma statistikasy we gözegçilik
- [ ] `[V2]` Marketplace: bir sahypada köp satyjy, komissiýa, satyjy tölegleri

## 1.4. Integrasiýalar

- [x] `[MVP]` E-poçta
- [ ] `[V1]` SMS provider
- [ ] `[V1]` Ýerli bank ekwaýringi
- [ ] `[V1]` Telegram bildirişleri
- [ ] `[V1]` Google Analytics, Meta Pixel (razylyk bilen)
- [ ] `[V1]` Daşary ulgamlar üçin public API we webhook
- [ ] `[V2]` Halkara töleg provideri
- [ ] `[V2]` Kurýer gullugynyň API-si
- [ ] `[V2]` Sosial mediadan satuw

---

# 2-nji bölüm. Umumy taýýarlyk

## U0. Gurşaw we taslama

- [x] .NET 10 SDK gurmak (10.0.107 gurlan)
- [ ] Kompýuterdäki .NET paketlerini düzetmek: SDK Ubuntu-dan, targeting pack-ler Microsoft-yň Debian 13 çeşmesinden gurlan, şonuň üçin SDK olary tapmaýar (`NETSDK1226`). Düzedilenden soň `backend/Directory.Build.props`-daky `RestoreEnablePackagePruning` we `UseAppHost` sazlamalaryna täzeden garamak
- [x] Docker gurmak (29.9.2 gurlan)
- [x] Node.js-i LTS wersiýa täzelemek: nvm-de v24.18 (`frontend/.nvmrc`). Nuxt `>=24.15` talap edýär, `PATH`-daky `/usr/local/lib/nodejs` bolsa v24.14, şonuň üçin `nvm use` gerek
- [x] pnpm gurmak: 11.9.0, wersiýasy `packageManager`-da berkidilen; täze kompýuterde `corepack enable`
- [x] `git init` (ýerli repozitoriý, `main` şahasy)
- [ ] Uzakdaky repozitoriý döretmek
- [x] Bukja gurluşyny döretmek: `backend/`, `deploy/`
- [x] Bukja gurluşyny döretmek: `frontend/`, `docs/`
- [x] `.gitignore`, `.editorconfig` goşmak
- [x] `.gitattributes` we kök `README.md` goşmak
- [ ] Şaha strategiýasyny we Conventional Commits kadasyny kesgitlemek — ✅ Conventional Commits (`commit-msg` hook); şaha strategiýasy ýok
- [x] PR şablonyny we „tamamlandy“ kesgitlemesini ýazmak
- [x] `docker-compose.yml`: PostgreSQL, Redis, MinIO, Mailpit, Seq
- [ ] Domen modelini we ER diagrammasyny taýýarlamak
- [ ] Arhitektura kararlaryny ADR görnüşinde `docs/adr/` bukjasyna ýazmak — ✅ frontend (ADR 0001); backend kararlary ýok
- [ ] 🔒 `main` şahasyny goramak: hökmany PR we CI, göni push gadagan
- [ ] 🔒 Git hesaplarynda 2FA-ny hökmany etmek
- [ ] 🔒 `gitleaks` pre-commit hook we CI barlagy (gizlin maglumatlaryň kod bilen düşmezligi üçin)
- [x] 🔒 `.env.example` döretmek; hakyky `.env` faýllary hiç haçan commit edilmeýär
- [ ] 🔒 Howp modelini düzmek (STRIDE): aktiwler, hüjüm ýollary, ynam serhetleri
- [ ] 🔒 Maglumatlary toparlara bölmek: şahsy maglumat, töleg maglumaty, gizlin maglumat, açyk maglumat
- [ ] 🔒 Howpsuzlyk ölçegi hökmünde OWASP ASVS Level 2 we API Security Top 10 kabul etmek

---

# 3-nji bölüm. Backend (ASP.NET Core)

## B0. Solution gurluşy

- [x] Solution we iki taslama döretmek: `ComTest.Api`, `ComTest.UnitTests`
- [x] API-ni modullara bölmek: `Application`, `Domain`, `Infrastructure`, `Modules/*`, `Shared`
- [x] `Directory.Build.props`: `Nullable`, `TreatWarningsAsErrors`, analizatorlar
- [x] `Directory.Packages.props`: paket wersiýalaryny bir ýerden dolandyrmak
- [x] Modullar arasyndaky baglanyşyk kadalaryny kesgitlemek (diňe public kontrakt arkaly)
- [x] Arhitektura testleri: modul serhetleriniň bozulmazlygy (NetArchTest)
- [x] `dotnet format` we kod stili barlaglary
- [x] `Result<T>` nagşy we domen ýalňyşlyklary
- [x] Command/query handler nagşy (täjirçilik kitaphanasyz)
- [x] 🔒 `packages.lock.json` we `RestoreLockedMode` bilen paket wersiýalaryny berkitmek

## B1. Umumy infrastruktura

- [x] Sazlamalar: `IOptions<T>`, işe başlanda `ValidateOnStart` bilen barlamak
- [x] Serilog bilen gurluşly log, `CorrelationId`
- [x] OpenTelemetry: trace we metrikalar
- [x] Global ýalňyşlyk işleýjisi, `ProblemDetails` (RFC 9457)
- [x] FluentValidation we awtomatik walidasiýa filtri
- [x] API wersiýalamak (`/api/v1`)
- [x] OpenAPI + Scalar dokumentasiýasy
- [x] Sahypalama, süzmek, tertipleşdirmek üçin umumy kontrakt
- [x] Health check-ler: `/health/live`, `/health/ready`
- [x] Köp dilli jogaplar: `Accept-Language` we resurs faýllary
- [x] 🔒 Howpsuzlyk sözbaşylary: HSTS, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`, CSP
- [x] 🔒 CORS: diňe rugsat berlen domenler, `*` gadagan
- [x] 🔒 HTTPS-e mejbury geçirmek
- [x] 🔒 Rate limiting: umumy, endpoint, IP, ulanyjy we tenant boýunça
- [x] 🔒 Sorag göwrümi we wagt çäkleri (Kestrel limits, request timeouts)
- [x] 🔒 Production-da stack trace we içerki ýalňyşlyk jikme-jikliklerini gizlemek
- [x] 🔒 `Server` sözbaşysyny aýyrmak
- [x] 🔒 Forwarded headers: diňe ynamdar proxy-lerden kabul etmek
- [x] 🔒 Loglarda şahsy maglumatlary, tokenleri we açar sözlerini maskalamak
- [x] 🔒 Data Protection açarlaryny umumy saklaýyşda şifrlenen görnüşde saklamak
- [x] 🔒 Production-da OpenAPI-ni öçürmek ýa-da ygtyýarlandyrma bilen ýapmak

## B2. Maglumatlar bazasy

- [x] EF Core + Npgsql sazlamak, `snake_case` atlandyrma
- [x] Bazalaýyn `Entity`, audit meýdanlary (`CreatedAt`, `UpdatedBy` we ş.m.)
- [x] `Guid.CreateVersion7()` bilen açarlar
- [x] Soft delete we global query filter
- [x] Optimistik concurrency (`xmin`)
- [x] Köp dilli meýdanlar üçin terjime tablisalary
- [x] Pul üçin `Money` value object (`numeric(18,2)` + pul birligi)
- [x] Migrasiýa strategiýasy: awtomatik däl, aýratyn ädimde ulanmak
- [x] Dev we test üçin başlangyç maglumatlar (seed)
- [x] Transactional Outbox nagşy (domen wakalary üçin)
- [x] 🔒 Aýratyn DB ulanyjylary: migrasiýa üçin we programma üçin (iň az ygtyýar)
- [x] 🔒 DB-a TLS arkaly birikmek
- [x] 🔒 Raw SQL-i diňe parametrler bilen ulanmak; `FromSqlRaw`-a setir birleşdirmek gadagan
- [x] 🔒 DB ýalňyşlyklaryny müşderä görkezmezlik

## B3. Autentifikasiýa we ygtyýarlandyrma

- [x] ASP.NET Core Identity sazlamak
- [x] Hasaba alynmak, e-poçta/telefon tassyklamak
- [x] Login, logout, access + refresh token akymy
- [x] Aktiw sessiýalar sanawy we sessiýany ýatyrmak
- [x] Rollar: `PlatformAdmin`, `StoreOwner`, `Manager`, `Staff`, `Customer`
- [x] Permission esasly policy ulgamy
- [ ] Profili üýtgetmek: at, dil, e-poçtany täzeden tassyklama bilen çalyşmak (häzir diňe `GET /account/me`)
- [ ] `[V1]` Google bilen girmek (OIDC)
- [x] 🔒 Access token ömri ≤ 15 minut; `iss`, `aud`, `exp` we algoritm berk barlanýar
- [x] 🔒 JWT asimmetrik açar bilen (ES256/RS256) gollanýar, `kid` bilen açar rotasiýasy
- [x] 🔒 Refresh token DB-de diňe hash görnüşinde saklanýar
- [x] 🔒 Refresh token rotation we gaýtadan ulanylandygyny ýüze çykarmak (bolsa, ähli sessiýa ýatyrylýar)
- [x] 🔒 Refresh cookie: `HttpOnly`, `Secure`, `SameSite`, çäkli `Path`; CSRF goragy
- [x] 🔒 Açar sözi syýasaty: uzynlygy ≥ 12, syzan açar sözleriň sanawy boýunça barlag
- [x] 🔒 Açar sözi hash: Argon2id ýa-da PBKDF2 (≥ 210 000 iterasiýa)
- [x] 🔒 Hasaby wagtlaýyn gulplamak we login üçin aýratyn rate limit
- [x] 🔒 Ulanyjy barlygyny ýüze çykarmagyň öňüni almak (login, reset, hasaba alyşda birmeňzeş jogap)
- [x] 🔒 Açar sözüni dikeltmek: bir gezeklik, gysga möhletli token; soňra ähli sessiýa ýatyrylýar
- [x] 🔒 TOTP 2FA we dikeldiş kodlary; işgärler üçin hökmany
- [x] 🔒 SMS OTP: synanyşyk sany, möhlet we iberiş ýygylygy çäklendirilýär
- [x] 🔒 Deny-by-default: `FallbackPolicy` — her endpoint ygtyýar talap edýär, açyklary diňe aýdyň `[AllowAnonymous]`
- [x] 🔒 Resource esasly barlag: başga ulanyjynyň sargydyna/salgysyna giriş (IDOR) gadagan
- [x] 🔒 Roly ýa-da açar sözi üýtgände sessiýalary täzeden barlamak (`SecurityStamp`)
- [x] 🔒 Giriş wakalaryny audit etmek: üstünlikli, şowsuz, gulplanan, 2FA
- [x] Test: tokeniň ähli ýaşaýyş aýlawy we hüjüm ssenariýalary

## B4. Multi-tenant we dükan dolandyryşy

- [x] `Tenant` modeli we tarif çäkleri
- [x] Tenant kesgitlemek: subdomen ýa-da öz domeni
- [x] `ITenantContext` we middleware
- [x] Firmany hasaba almak we başlangyç sazlamalar
- [x] Dükan sazlamalary: logotip, aragatnaşyk, iş wagty, pul birligi, dil
- [x] Işgärleri çakylyk bilen goşmak
- [ ] `[V1]` Öz domenini baglamak (DNS barlagy, awtomatik TLS)
- [x] 🔒 Ähli tenant tablisalarynda `TenantId` we global query filter
- [x] 🔒 PostgreSQL RLS: birikme açylanda `set_config('app.tenant_id')`; programma ulanyjysynda `BYPASSRLS` ýok (PgBouncer ulanylsa, `session` režimi gerek)
- [x] 🔒 Tokendäki tenant bilen soragdaky tenantyň gabat gelmegini barlamak
- [x] 🔒 Keş açarlaryna, fon işlerine we faýl ýollaryna `TenantId` goşmak
- [ ] 🔒 Gözleg indeksine `TenantId` goşmak (B15-de gözleg motory bilen)
- [x] 🔒 `IgnoreQueryFilters` ulanylyşyny analizator/kod syny bilen gözegçilikde saklamak
- [ ] 🔒 Integrasiýa testi: iki tenant arasynda maglumat syzmasynyň ýoklugyny her endpoint üçin barlamak

## B5. Katalog

- [x] `Category` (agaç gurluşy), `Brand`, `Product`, `ProductVariant`, `Attribute`
- [x] Haryt CRUD: garalama/çap edilen/arhiw ýagdaýlary
- [x] Köp dilli at, düşündiriş, slug
- [x] Baha, köne baha, özüne düşýän baha, salgyt topary
- [x] Kategoriýa boýunça dinamiki häsiýetler
- [x] Sanaw: sahypalama, süzgüç, tertipleşdirme
- [ ] Storefront sanawynda häsiýet boýunça süzgüç (häzir kategoriýa, marka, baha we tekst bar)
- [x] Baha üýtgemeleriniň taryhy
- [x] Storefront üçin aýratyn read modeli we keş
- [ ] `[V1]` Excel/CSV importy (fon işi, ýalňyşlyk hasabaty bilen)
- [x] 🔒 Request/response üçin aýratyn DTO (mass assignment we artykmaç maglumat öňüni almak)
- [x] 🔒 Özüne düşýän baha ýaly içerki meýdanlar açyk API-de görkezilmeýär
- [x] 🔒 Tertipleşdirme meýdanlary üçin allowlist; sahypa ululygynda ýokarky çäk
- [x] 🔒 HTML düşündirişi serwerde arassalamak (`HtmlSanitizer`)

## B6. Faýllar we suratlar

- [x] `IFileStorage` abstraksiýasy (S3/MinIO)
- [x] Presigned URL bilen ýüklemek
- [x] Suratlary WebP formatyna we dürli ululyklara öwürmek (fon işi)
- [x] CDN we uzak möhletli keş sözbaşylary
- [x] Taşlanan we ret edilen ýüklemeleri arassalamak işi
- [x] 🔒 Faýl görnüşini giňeltme boýunça däl, „magic bytes“ boýunça barlamak (allowlist)
- [x] 🔒 Faýl ululygyna çäk, tenant boýunça ýer kwotasy
- [x] 🔒 Suratlary täzeden kodlamak we EXIF maglumatlaryny aýyrmak
- [x] 🔒 Tötänleýin faýl atlary; path traversal öňüni almak
- [x] 🔒 SVG ýüklemek gadagan ýa-da arassalanan; goşmaça domenden bermek
- [x] 🔒 Bucket ýapyk; diňe CDN/presigned URL arkaly giriş
- [ ] 🔒 `[V1]` ClamAV bilen wirus barlagy

## B7. Ammar

- [x] `Warehouse`, `StockItem`, `StockMovement`
- [x] Galyndy hereketleri: girdeji, çykdajy, düzediş, rezerw
- [x] Sargyt üçin rezerw (TTL bilen) we möhleti geçenini boşatmak
- [x] Az galan haryt duýduryşy
- [ ] `[V1]` Köp ammar, ammarlar arasynda geçirim
- [ ] `[V1]` Inwentarizasiýa
- [ ] `[V1]` Üpjünçiler we satyn alma sargytlary
- [x] 🔒 Galyndy üýtgemeleriniň ählisi kim tarapyndan edilendigi bilen ýazga alynýar
- [x] Test: bir wagtda köp sargytda galyndynyň minus bolmazlygy (race condition)

## B8. Sebet we checkout

- [x] Myhman sebedi (gizlin kukili ID) we ulanyjy sebedi; ulgama girende birleşdirmek
- [x] Sebetdäki baha/galyndy üýtgemelerini barlamak we duýdurmak
- [x] Checkout hasaplamasy: harytlar, eltip bermek
- [ ] Checkout-a arzanladyş (B12) we salgyt hasaplamasyny goşmak (häzir ikisi hem 0)
- [x] Salgy we eltip bermek görnüşini saýlamak
- [x] 🔒 Baha, arzanladyş we jemi diňe serwerde hasaplanýar; müşderiden gelen baha kabul edilmeýär
- [x] 🔒 Mukdar üçin walidasiýa: otrisatel, nol, aşa uly bahalar
- [x] 🔒 Sargyt döretmekde idempotency key (iki gezek basylanda iki sargyt bolmaz)
- [x] 🔒 Sargyt spamynyň öňüni almak: rate limit, telefon boýunça açyk sargyt çägi
- [ ] 🔒 Sargyt spamyna garşy bot goragy we myhman üçin telefon tassyklamak (SMS provideri gerek)

## B9. Sargytlar

- [x] `Order`, `OrderItem`, ýagdaý taryhy
- [x] Ýagdaý maşyny: `Pending → Confirmed → Packed → Shipped → Delivered`, `Cancelled`, `Returned`
- [x] Harytlaryň at we baha nusgasyny (snapshot) sargytda saklamak
- [x] Ýatyrylanda galyndyny yzyna goýmak
- [x] Admin üçin sanaw, süzgüç, jikme-jiklik, bellikler
- [x] Domen wakalary: `OrderCreated`, `OrderPaid`, `OrderShipped`
- [ ] `[V1]` Faktura we goşma haty (PDF; QuestPDF ygtyýarnamasyny barlamak)
- [ ] `[V1]` Yzyna gaýtarma (RMA) akymy
- [x] 🔒 Sargyt belgisi yzygiderli we çaklap bolýan däl
- [x] 🔒 Myhman sargydyny yzarlamak gollanan token arkaly
- [x] 🔒 Ýagdaý geçişleri rol boýunça ygtyýarlandyrylýar we audit edilýär

## B10. Tölegler

**Nagt (MVP)**

- [x] `IPaymentProvider` abstraksiýasy we `Payment`, `Refund` modelleri
- [x] Alanda nagt tölemek
- [x] Bank geçirimi: el bilen tassyklamak, audit bilen

**Onlaýn (V1)**

- [ ] Ýerli bank ekwaýringini birikdirmek (redirect/hosted sahypa)
- [x] Doly we bölekleýin pul gaýtarma
- [ ] Tölegleri bank hasabaty bilen deňeşdirmek (reconciliation, her gün)
- [ ] Tamamlanmadyk tölegleriň ýagdaýyny barlamak we möhleti geçenleri ýatyrmak
- [ ] `[V2]` Halkara provider

**Howpsuzlyk**

- [ ] 🔒 Kart maglumatlary serwere düşmeýär we saklanmaýar (hosted sahypa, PCI DSS SAQ A derejesi)
- [ ] 🔒 Webhook goly, wagt belgisi we gaýtalanmaga garşy barlag
- [x] 🔒 Sargyt diňe serwer tarapyndan tassyklanandan soň tölenen bolýar; brauzeriň gaýdyp gelmegine ynanylmaýar (onlaýn töleg goşulanda täzeden barlanýar)
- [ ] 🔒 Gelen mukdar we pul birligi sargyt bilen deňeşdirilýär
- [x] 🔒 Ähli töleg amallarynda idempotency; iki gezek tutulmagyň öňüni almak
- [x] 🔒 Pul gaýtarma aýratyn ygtyýar talap edýär; uly mukdarlar üçin ikinji tassyk
- [x] 🔒 Töleg ýagdaý üýtgemeleri üýtgedip bolmaýan žurnala ýazylýar
- [ ] 🔒 Bank açarlary diňe secret manager-de

## B11. Eltip bermek

- [x] Eltip bermek zolaklary (welaýat, etrap, şäher) we tarifler
- [x] Mugt eltip bermegiň çägi
- [x] Dükandan özi alyp gitmek
- [x] Sargyt yzarlaýyş maglumaty
- [ ] `[V1]` Kurýerlere bellemek we kurýer interfeýsi üçin API
- [ ] `[V2]` Kurýer gullugynyň integrasiýasy
- [ ] 🔒 Kurýer diňe özüne bellenen sargydyň zerur maglumatlaryny görýär

## B12. Arzanladyşlar we wepalylyk

- [ ] Promokod: göterim, kesgitli mukdar, mugt eltip bermek
- [ ] Şertler: möhlet, iň az jemi, kategoriýa, ulanyş çägi
- [ ] Awtomatik aksiýalar, gysga möhletli arzanladyşlar
- [ ] Arzanladyşlaryň utgaşma kadalary
- [ ] Bonus ballary we sowgat kartalary
- [ ] `[V2]` Referal programmasy
- [ ] 🔒 Promokod saýlamaga garşy rate limit; çaklap bolmaýan kodlar
- [ ] 🔒 Ulanyş çägi atomik amal bilen barlanýar (race condition)
- [ ] 🔒 Bonus we sowgat kartasy balansy üýtgedilmeýän žurnal esasynda hasaplanýar
- [ ] 🔒 Jemi nola düşüp bilýän birleşmeleri test bilen barlamak

## B13. Bildirişler

- [ ] Umumy bildiriş gullugy, şablonlar (köp dilli)
- [x] E-poçta (MailKit, fon nobaty, gaýtalamak)
- [ ] Durnukly e-poçta nobaty: häzirki nobat RAM-da, programma täzeden başlasa iberilmedik hatlar ýitýär
- [x] SMS provider abstraksiýasy
- [ ] SignalR: admin üçin täze sargyt bildirişi, müşderi üçin ýagdaý üýtgemesi
- [ ] `[V1]` Telegram bildirişleri, web push
- [ ] `[V1]` Bildiriş sazlamalary we ýazylmadan çykmak — API ýok (B13)
- [ ] 🔒 SPF, DKIM, DMARC sazlamak
- [ ] 🔒 Şablonlarda ulanyjy maglumatlaryny encode etmek (HTML/şablon injection)
- [ ] 🔒 SMS býujetini goramak: nomer, IP we tenant boýunça çäk (SMS pumping)
- [ ] 🔒 SignalR hub ygtyýarlandyrmasy we tenant toparlary

## B14. Synlar, halanlarym, CMS

- [ ] Halanlarym sanawy
- [ ] Syn we baha: diňe satyn alan müşderi, moderasiýa
- [ ] Sorag-jogap
- [ ] CMS sahypalary, bannerler, menýular, blog
- [ ] 🔒 Syn teksti arassa tekst hökmünde saklanýar we görkezilýär
- [ ] 🔒 Syn spamyna garşy rate limit
- [ ] 🔒 CMS HTML-i serwerde arassalanýar

## B15. Gözleg we teklipler

- [ ] PostgreSQL FTS + `pg_trgm` bilen başlangyç gözleg (MVP)
- [ ] Meilisearch/Elasticsearch-e geçmek (V1)
- [ ] Outbox wakalary arkaly indeksi täzelemek
- [ ] Sinonimler, awtomatik doldurma, köp dilli analiz
- [ ] Meňzeş harytlar, köp satylýanlar
- [ ] `[V2]` Şahsy teklipler (AI)
- [ ] 🔒 Gözlegde tenant süzgüji serwerde berk goýulýar (tenant token)
- [ ] 🔒 Gözleg motory içerki torda, açyk internete çykmaýar
- [ ] 🔒 Gözleg endpoint-ine rate limit we sorag uzynlygyna çäk

## B16. Hasabatlar

- [ ] Dashboard görkezijileri: satuw, sargyt, ortaça çek, köp satylýanlar
- [ ] Satuw, haryt, müşderi, ammar hasabatlary — API ýok (B16)
- [ ] Hasabatlar üçin materialized view we fon hasaplamasy
- [ ] Excel eksporty (ClosedXML, fon işi)
- [ ] Platforma derejesindäki statistika
- [ ] 🔒 Excel/CSV formula injection (`=`, `+`, `-`, `@`) goragy
- [ ] 🔒 Eksport faýllary gysga möhletli, diňe eýesine elýeterli
- [ ] 🔒 Hasabatlar ygtyýar boýunça: gazanç maglumatlary diňe eýesine

## B17. Integrasiýalar

- [ ] Public API we API açarlary (scope bilen)
- [ ] Çykýan webhook-lar: gaýtalamak, žurnal
- [ ] Hangfire: fon we meýilleşdirilen işler, gaýtalamak
- [ ] `[V2]` 1C/ERP sinhronlaşdyrmasy
- [ ] 🔒 API açarlary diňe hash görnüşinde saklanýar; açar boýunça rate limit we rotasiýa
- [ ] 🔒 Webhook HMAC bilen gollanýar
- [ ] 🔒 Webhook URL üçin SSRF goragy: içerki IP-ler, metadata salgylary, redirect-ler gadagan
- [ ] 🔒 Hangfire dashboard diňe platforma admini üçin
- [ ] 🔒 Import faýllarynda ululyk, setir sany we zip bomb çäkleri

## B18. Öndürijilik we giňelmek

- [ ] Haýal soraglary tapmak (`pg_stat_statements`), indeksleri optimizirlemek
- [ ] N+1 soraglaryň öňüni almak, `AsNoTracking`, proýeksiýalar
- [ ] Uly sanawlarda keyset sahypalama
- [ ] `HybridCache` (Redis) we keşi dogry ýatyrmak
- [ ] Output caching, response compression
- [ ] PgBouncer bilen birikme pool-y
- [ ] Hasabatlar üçin read replica
- [ ] Stateless API: birnäçe nusga, SignalR Redis backplane
- [ ] Daşarky hyzmatlar üçin `Microsoft.Extensions.Http.Resilience` (timeout, retry, circuit breaker)
- [ ] 🔒 Uly soraglar bilen DoS öňüni almak: sorag wagty çägi, statement timeout
- [ ] 🔒 Redis parol/TLS bilen, içerki torda

## B19. Testler

- [x] Unit testler: domen we biznes kadalary (xUnit)
- [x] Integrasiýa testleri: Testcontainers bilen hakyky PostgreSQL we MinIO
- [ ] Integrasiýa testleri: Testcontainers bilen Redis (K4 ulanylanda)
- [x] API testleri: `WebApplicationFactory`
- [x] Doly ssenariý testi: hasaba alyş → sebet → sargyt → töleg → eltip bermek
- [x] Race condition testleri: galyndy, töleg
- [ ] Race condition testleri: promokod (B12 bilen)
- [ ] OpenAPI kontrakt barlagy — ✅ her endpoint jogap görnüşini yglan edýär (arhitektura testi); dokument bilen hakyky jogaplaryň awtomatik deňeşdirmesi ýok
- [ ] CI-da örtük hasabaty we iň az çäk (biznes logikasy üçin ≥ 80%)
- [ ] 🔒 Ygtyýarlandyrma matrisasy testi: her endpoint × her rol
- [x] 🔒 Her endpoint-iň ygtyýar talap edýändigini awtomatik barlamak
- [x] 🔒 Tenant üzňeligi test toplumy
- [x] 🔒 Regressiýa testi: tapylan her howpsuzlyk kemçiligi üçin (şu wagta çenli tapylanlar örtüldi; kada dowam edýär)

## B20. Howpsuzlyk gutarnykly barlagy

- [ ] 🔒 `dotnet list package --vulnerable --include-transitive` CI-da
- [ ] 🔒 Dependabot/Renovate bilen paketleri täzelemek
- [ ] 🔒 CodeQL (SAST) CI-da
- [ ] 🔒 Şahsy maglumatlaryň sütün derejesinde şifrlenmegi (zerur ýerlerde)
- [ ] 🔒 Maglumat saklama möhletleri we awtomatik arassalamak
- [x] 🔒 Audit žurnaly üýtgedip bolmaýan görnüşde saklanýar (programma DB ulanyjysy üçin diňe INSERT)
- [ ] 🔒 Şübheli hereketlere duýduryş: köp şowsuz giriş, tenant bozulma synanyşygy, töleg ýalňyşlyklary
- [ ] 🔒 Open redirect: `returnUrl` üçin allowlist
- [ ] 🔒 Biznes logikasyna hüjümleri barlamak: promokod, bonus, yzyna gaýtarma, mukdar manipulýasiýasy
- [ ] 🔒 OWASP API Security Top 10 boýunça her bent barlanýar
- [ ] 🔒 OWASP ASVS Level 2 boýunça çek-list doldurulýar

---

# 4-nji bölüm. Frontend (Vue 3, TypeScript, Element Plus, Tailwind CSS)

## F0. Taslama gurluşy

- [x] pnpm workspace monorepo: `apps/storefront`, `apps/admin`, `packages/ui`, `packages/api-client`, `packages/config`, goşmaça `packages/auth` (ADR 0001)
- [x] `apps/admin`: Vite + Vue 3 + TS
- [x] `apps/storefront`: Nuxt + TS (K6 kararyna görä)
- [x] `strict` TypeScript, `vue-tsc` barlagy
- [x] ESLint (flat config), Prettier
- [x] Husky, lint-staged, commitlint
- [x] Feature esasly bukja gurluşy
- [x] Elementleri zerur bolanda ýüklemek: `unplugin-vue-components` + `ElementPlusResolver`
- [x] 🔒 `pnpm-lock.yaml` döredildi; täze paket azyndan bir hepde garaşýar (`minimumReleaseAge`)
- [ ] 🔒 CI-da `--frozen-lockfile` (P2-de) — ✅ workflow ýazyldy (`.github/workflows/frontend.yml`); uzakdaky repo ýok, entek işledilmedi
- [x] 🔒 Paketleriň install skriptleri diňe rugsat berlen sanaw boýunça

## F1. Dizaýn ulgamy

- [x] Dizaýn tokenleri: reňk, aralyk, şrift, radius, kölege
- [x] Tailwind sazlamak we tokenleri Element Plus CSS üýtgeýjileri bilen birleşdirmek
- [x] Tailwind preflight bilen Element Plus stilleriniň çaknyşmagynyň öňüni almak (CSS `@layer` tertibi)
- [x] Element Plus temasy we dil faýllary (tk üçin öz terjimämiz, ru, en)
- [x] Gara tema
- [x] Umumy elementler: `AppButton`, `AppTable`, `AppForm`, `AppDialog`, `PageHeader`
- [x] Boş, ýüklenýän (skeleton) we ýalňyşlyk ýagdaýlary
- [x] Pul, sene, telefon formatlaýjylary
- [x] Responsiw sazlamalar we mobile-first kadalary
- [x] Storybook/Histoire bilen elementleri resminamalaşdyrmak

## F2. Esasy gatlaklar

- [x] OpenAPI-den TypeScript tiplerini we API müşderisini awtomatik döretmek
- [x] HTTP gatlagy: bazalaýyn URL, `Accept-Language`, `CorrelationId`, ýalňyşlyklary işlemek
- [x] TanStack Query: server maglumatlary, keş, täzeden synanyşmak
- [x] Pinia: diňe müşderi ýagdaýy (auth, sebet, UI) (sebet F5-de goşulýar)
- [x] Vue Router: lazy loading, layout-lar, route meta
- [x] vue-i18n: tk, ru, en; dil URL-de saklanýar
- [x] Zod bilen forma walidasiýasy; serwer ýalňyşlyklaryny meýdanlara baglamak
- [x] Global ýalňyşlyk işleýjisi, Error Boundary, 404/403/500 sahypalary
- [x] Sentry bilen brauzer ýalňyşlyklaryny yzarlamak (DSN berlende işleýär)
- [ ] Sentry: Nuxt serwer ýalňyşlyklary we hakyky DSN bilen synag — brauzer tarapy galp transport bilen barlandy; serwer tarapy we hakyky DSN ýok
- [x] 🔒 Access token diňe RAM-da; `localStorage`/`sessionStorage`-da token saklamak gadagan
- [x] 🔒 Silent refresh: bir wagtda diňe bir refresh sorag (single-flight)
- [x] 🔒 Cookie diňe öz API domenine iberilýär (`withCredentials` çäkli)
- [x] 🔒 `VITE_*`/`NUXT_PUBLIC_*` üýtgeýjilerinde gizlin maglumat ýok (bular brauzerde görünýär)
- [x] 🔒 Route guard diňe UX üçin; hakyky barlag serwerde
- [x] 🔒 Sentry-de şahsy maglumatlary we tokenleri arassalamak

## F3. Autentifikasiýa sahypalary

- [x] Hasaba alynmak, ulgama girmek, ulgamdan çykmak
- [x] E-poçta/telefon tassyklamak (OTP girizmek elementi)
- [x] Açar sözüni dikeltmek
- [x] Admin üçin 2FA gurnamak we girmek, dikeldiş kodlary
- [x] Aktiw sessiýalar sahypasy
- [x] Rol/permission esasly görkeziş: `v-can` direktiwasy
- [x] 🔒 Giriş ýalňyşlygynda umumy habar (ulanyjy bar/ýok açyk aýdylmaýar)
- [x] 🔒 `returnUrl`-y diňe içerki ýollara rugsat bermek (open redirect)
- [x] 🔒 Ulgamdan çykanda ähli ýagdaý, keş we query cache arassalanýar
- [x] 🔒 `autocomplete` atributlary dogry; açar sözi dolandyryjylary bilen sazlaşykly

## F4. Storefront: katalog

- [x] Layout: header, menýu, gözleg, sebet, dil saýlamak, footer
- [ ] Baş sahypa: bannerler, kategoriýalar, täze we köp satylýan harytlar — ✅ kategoriýalar, täze harytlar; bannerler (B14) we köp satylýanlar (B15) üçin API ýok
- [x] Kategoriýa sahypasy: süzgüçler, tertipleşdirme, sahypalama (häsiýet süzgüji API-de ýok)
- [x] Süzgüç ýagdaýy URL-de saklanýar
- [x] Haryt kartasy
- [ ] Haryt sahypasy: galereýa, wariant saýlamak, häsiýetler, synlar — ✅ galereýa, wariant saýlamak, häsiýetler; synlar üçin API ýok (B14)
- [x] Gözleg sahypasy we awtomatik doldurma
- [x] Telefon üçin süzgüç paneli we aşaky nawigasiýa
- [x] `[V1]` Deňeşdirmek, soňky görlenler, meňzeş harytlar (meňzeş: şol bir kategoriýa boýunça)
- [x] 🔒 CMS/düşündiriş HTML-i diňe DOMPurify-dan soň görkezilýär
- [x] 🔒 SSR: ulanyjy maglumatlary soraglar arasynda garyşmaýar (global ýagdaý ýok, Pinia her sorag üçin aýratyn)
- [x] 🔒 SSR payload-ynda token ýa-da içerki maglumat ýok

## F5. Storefront: sebet we checkout

- [x] Sebet sahypasy we kiçi sebet (drawer)
- [x] Myhman sebedini ulanyjy sebedi bilen birleşdirmek
- [x] Baha/galyndy üýtgände duýduryş
- [x] Checkout ädimleri: aragatnaşyk → salgy → eltip bermek → töleg → tassyklama
- [x] Salgy saýlamak: welaýat, etrap, şäher
- [ ] Töleg sahypasy we bankdan gaýdyp gelmek — ✅ nagt we bank geçirimi görkezmeleri; onlaýn töleg üçin ekwaýring ýok (B10), gaýdyp gelmek ýoly barlanmady
- [x] Sargyt tassyklandy sahypasy
- [x] Sargyt düwmesini iki gezek basmagyň öňüni almak
- [x] 🔒 Ekranda görkezilýän jemi diňe serwer jogabyndan alynýar
- [x] 🔒 Töleg sahypalarynda üçünji tarap skriptleri ýok
- [x] 🔒 Sargyt tassyklamasy serwerden barlanýar; URL parametrine ynanylmaýar

## F6. Storefront: şahsy otag

- [ ] Profil we salgylar — ✅ profili görmek, salgylar; profili üýtgetmek üçin API ýok (B3)
- [x] Sargytlar we yzarlaýyş
- [ ] Hakyky wagtda ýagdaý täzelenmesi (SignalR) — ✅ açyk sahypa 60 sekuntda täzelenýär (polling); SignalR üçin backend ýok (B13)
- [ ] `[V1]` Halanlarym, synlar, bonus ballary, yzyna gaýtarma — API ýok (B12, B14)
- [ ] `[V1]` Bildiriş sazlamalary
- [ ] `[V1]` Maglumatlary göçürip almak we hasaby pozmak — API ýok
- [ ] 🔒 Telefon, e-poçta ýa-da açar sözi üýtgedilende täzeden tassyklamak — ✅ açar sözi häzirkisi bilen, telefon SMS bilen; e-poçtany çalyşmak üçin API ýok

## F7. Admin: esas

- [x] Layout: gapdal menýu, header, breadcrumbs, dükan saýlamak
- [x] Rol boýunça menýu we sahypalar
- [x] Umumy maglumat tablisasy: server tarapynda sahypalama, süzgüç, sütün saýlamak
- [x] Uly sanawlar üçin `el-table-v2`
- [x] Umumy forma drawer/dialog nagşy
- [x] Dashboard: görkezijiler, ECharts grafikleri (sanlar bar API-lerden alynýar; satuw mukdary B16-a garaşýar)
- [x] Global gözleg, klawiatura gysga ýollary
- [ ] Hakyky wagtda täze sargyt bildirişi — ✅ 60 sekuntda bir barlag (polling); SignalR üçin backend ýok (B13)
- [ ] 🔒 Admin panel aýratyn domende; gözleg ulgamlaryndan ýapyk (`noindex`) — ✅ `noindex`; aýratyn domen deploy bilen (P1/P3)
- [x] 🔒 Hereketsiz sessiýanyň awtomatik gutarmagy

## F8. Admin: harytlar we ammar

- [x] Haryt sanawy we köpçülikleýin hereketler
- [x] Haryt formasy: esasy maglumat, köp dilli meýdanlar, wariantlar, suratlar, SEO (kategoriýa häsiýetleri: API baglanyşygy bermeýär, ähli häsiýetler görkezilýär)
- [x] Surat ýüklemek: süýräp goýmak, tertip, ýüklenme görkezijisi
- [x] Formatlanan tekst redaktory (TipTap)
- [x] Kategoriýa agajy (süýräp tertipleşdirmek), markalar, häsiýetler
- [x] Ammar galyndysy, hereketler taryhy
- [x] Saklanmadyk üýtgeşmeler barada duýduryş
- [ ] `[V1]` Excel importy: mysal faýl, öňünden görkezmek, ýalňyşlyk hasabaty — API ýok (B5)
- [ ] `[V1]` Inwentarizasiýa, üpjünçiler — API ýok (B7)
- [x] 🔒 Redaktorda rugsat berlen teglere çäk goýmak
- [x] 🔒 Faýl ululygy/görnüşi müşderide hem barlanýar (esasy barlag serwerde)

## F9. Admin: sargytlar, müşderiler, sazlamalar

- [x] Sargyt sanawy: ýagdaý boýunça süzgüç
- [x] Sargyt jikme-jikligi: ýagdaý üýtgetmek, taryh, bellikler
- [x] Müşderiler sanawy we profil (profil sargytlardan ýygnalýar, aýratyn API ýok)
- [ ] Dükan sazlamalary: umumy, töleg, eltip bermek, dil — ✅ umumy, eltip bermek, dil we pul; töleg sazlamalaryny okamak üçin API ýok
- [x] Işgärler we rollar: çakylyk, permission matrisasy (çakylyklaryň sanawy API-de ýok, diňe şu sahypada iberilenler görünýär)
- [ ] `[V1]` Faktura çap etmek, yzyna gaýtarma, pul gaýtarma — ✅ pul gaýtarma, yzyna gaýtarma ýagdaýy; faktura (PDF) we RMA ýok
- [x] 🔒 Howply hereketler (pozmak, pul gaýtarma) tassyklama talap edýär
- [ ] 🔒 Müşderiniň şahsy maglumatlary diňe ygtyýary bar işgärlere doly görkezilýär — ✅ interfeýsde gizlenýär; `/admin/orders` bolsa `orders.read` bilen hem şahsy maglumat berýär, backend-de düzetmeli

## F10. Admin: marketing we mazmun

- [ ] Promokodlar, aksiýalar — API ýok (B12–B14)
- [ ] Banner, baş sahypa, menýu dolandyrmak — API ýok (B12–B14)
- [ ] CMS sahypalary, blog — API ýok (B12–B14)
- [ ] Syn moderasiýasy — API ýok (B12–B14)
- [ ] SEO sazlamalary — ✅ haryt formasynda (F8); sahypa derejesindäki sazlama üçin API ýok
- [ ] E-poçta/SMS kampaniýalary — API ýok (B12–B14)

## F11. Admin: hasabatlar we audit

- [ ] Satuw, haryt, müşderi, ammar hasabatlary
- [ ] Sene aralygy saýlamak, deňeşdirme, Excel eksporty — API ýok (B16)
- [ ] Audit žurnaly sahypasy — okamak üçin API ýok
- [ ] API açarlary we webhook sazlamalary — API ýok (B17)
- [ ] 🔒 Gizlin açar diňe döredilende bir gezek görkezilýär — API ýok (B17)

## F12. Platforma admin paneli

- [ ] Firmalar: tassyklamak, togtatmak, tarifler — ✅ sanaw, togtatmak, işjeňleşdirmek; tassyklamak we tarifler üçin API ýok
- [ ] Abuna tölegleri — API ýok
- [ ] Platforma statistikasy we ulgam ýagdaýy — API ýok; diňe firma sany
- [ ] Global sazlamalar — API ýok
- [ ] 🔒 Aýratyn domen, hökmany 2FA, mümkin bolsa IP/VPN çäklendirmesi — ✅ hökmany 2FA; domen we IP deploy bilen
- [ ] 🔒 Tenant adyndan girmek (impersonation) aýdyň görkezilýär, wagty çäkli we audit edilýär — API ýok

## F13. SEO, öndürijilik, PWA

- [x] Her sahypa üçin meta, Open Graph, canonical
- [x] `hreflang` (tk, ru, en)
- [x] Schema.org: `Product`, `BreadcrumbList`, `Organization`
- [x] `sitemap.xml`, `robots.txt`
- [x] Sahypalar boýunça kod bölmek, bundle analizi we býujeti (baş sahypa ~205 KB gzip JS; býujet `e2e/performance.spec.ts`-de; analiz `nuxt analyze` bilen)
- [x] Suratlar: `srcset`, WebP/AVIF, lazy loading, ölçegler (CLS üçin) (WebP API-den, AVIF ýok; sanawlarda bir ölçeg, sebäbi API bir salgy berýär)
- [x] Core Web Vitals: LCP < 2.5 s, INP < 200 ms, CLS < 0.1 (laboratoriýa ölçegi, haýal telefon: LCP ~0,65 s, CLS ~0; hakyky ulanyjy maglumaty ýok)
- [ ] Lighthouse CI — ✅ `pnpm test:lighthouse` işleýär (öndürijilik 98–99, elýeterlilik 98–100, SEO 92–100); CI-da entek işledilmedi
- [x] Haýal internet üçin synag
- [ ] `[V1]` PWA: `vite-plugin-pwa`/`@vite-pwa/nuxt`, offline sahypa, push bildirişler — ✅ manifest, service worker, offline sahypa (goşmaça paketsiz); push üçin backend ýok
- [x] 🔒 Service worker şahsy API jogaplaryny keşlemeýär
- [ ] 🔒 Analitika we pixel-ler diňe razylykdan soň; şahsy maglumat iberilmeýär — häzir hiç hili analitika ýok; razylyk mehanizmi P7-de

## F14. Testler we elýeterlilik

- [x] Vitest + Vue Test Utils: composable, store, element testleri (191 test)
- [x] API-ni MSW bilen simulirlemek
- [x] Playwright E2E: satyn alyş akymy, admin akymlary (364 test: satyn alyş, admin, 2FA, API öçük, CSP; `e2e/stack.sh` aýratyn bazada)
- [x] Wizual regressiýa testi (esasy sahypalar) (`pnpm test:visual`; baza suratlary şu kompýuter üçin, CI-da täzeden döretmeli)
- [ ] Brauzerler we enjamlar boýunça synag — ✅ Chrome, telefon emulýasiýasy; Firefox we WebKit gurlan, ýöne ulgam kitaphanalary ýok (`sudo pnpm exec playwright install-deps`)
- [ ] Klawiatura bilen doly dolandyrmak, fokus tertibi — ✅ esasy akymlar synagdan geçdi; ähli sahypalar boýunça el bilen barlanmady
- [ ] Reňk kontrasty, `aria` atributlary, WCAG 2.2 AA — ✅ awtomatik barlag ähli sahypalarda arassa; ekran okaýjy bilen el synagy ýok
- [x] `axe-core` testleri (iki programmanyň ähli esasy sahypalary, ýagty we gara tema)
- [ ] Ähli terjimeleriň doly bolmagyny CI-da barlamak — ✅ test bar (açarlar, boş tekst, ýer tutujylar); CI-da entek işledilmedi

## F15. Frontend howpsuzlygy

- [x] 🔒 ESLint `vue/no-v-html` düzgüni; kadadan çykmalar diňe DOMPurify bilen
- [x] 🔒 Ulanyjy URL-leri üçin protokol barlagy (`javascript:` gadagan)
- [x] 🔒 Berk CSP: `script-src` nonce/hash, `object-src 'none'`, `base-uri` (storefront: nonce, sözbaşy; admin: `script-src 'self'`, meta teg)
- [ ] 🔒 Admin üçin `frame-ancestors` sözbaşysy: meta teg berip bilmeýär, web serwerde (P1)
- [x] 🔒 Element Plus-yň inline stilleri üçin `style-src` sazlamasyny synagdan geçirmek; ilki `Content-Security-Policy-Report-Only` (testler ýerine ýetirmek re­žiminde geçýär; storefront-da deslapky re­žim Report-Only, `NUXT_CSP_MODE=enforce` bilen açylýar)
- [x] 🔒 CSP bozulma hasabatlaryny ýygnamak (storefront `/_csp-report`; admin meta tegi hasabat iberip bilmeýär)
- [x] 🔒 Üçünji tarap skriptlerini iň az derejede saklamak; CDN-den alynanlara SRI (üçünji tarap skripti ýok)
- [x] 🔒 Source map-ler açyk çap edilmeýär (diňe Sentry-e) (döredilmeýär, test bilen barlanýar)
- [ ] 🔒 `pnpm audit` we Dependabot/Renovate CI-da — ✅ workflow we `dependabot.yml` ýazyldy; uzakdaky repo ýok, işledilmedi; audit: 1 pes, 2 orta, üçüsi hem Nuxt-yň build gurallarynda
- [x] 🔒 `localStorage`-da şahsy maglumat ýok
- [x] 🔒 Kod syny çek-listi: XSS, CSRF, açyk maglumat (`docs/frontend-kod-syny.md`)

---

# 5-nji bölüm. Production taýýarlygy

## P1. Konteýnerler

- [ ] Backend üçin multi-stage `Dockerfile`
- [ ] Frontend üçin `Dockerfile` (admin: Nginx; storefront: Node SSR)
- [ ] `docker-compose.prod.yml` ýa-da Kubernetes manifestleri
- [ ] Graceful shutdown we health probe-lar
- [ ] 🔒 Konteýnerler root däl ulanyjy bilen, `read-only` faýl ulgamy, `cap_drop: ALL`
- [ ] 🔒 Iň kiçi bazalaýyn suratlar (chiseled/distroless), wersiýa digest bilen berkidilýär
- [ ] 🔒 Trivy bilen surat barlagy; SBOM döretmek

## P2. CI/CD

- [ ] Pipeline: lint → build → test → surat → deploy
- [ ] Gurşawlar: `dev`, `staging`, `production`
- [ ] Migrasiýany deploy-dan öň aýratyn ädimde ulanmak; yza gaýdyş meýilnamasy
- [ ] Nol wagt togtamasy bilen deploy (rolling/blue-green)
- [ ] Çalt yza gaýtarmak (rollback) prosedurasyny synagdan geçirmek
- [ ] Feature flag-lar
- [ ] Staging-de awtomatik E2E
- [ ] 🔒 CI gizlin maglumatlary gurşaw boýunça aýratyn; PR-lardan production açarlaryna giriş ýok
- [ ] 🔒 GitHub Actions-lary commit SHA bilen berkitmek; iň az `permissions`
- [ ] 🔒 OIDC bilen gysga möhletli bulut girişi
- [ ] 🔒 Production deploy üçin el bilen tassyklama
- [ ] 🔒 CI-da hökmany barlaglar: gitleaks, CodeQL, paket we surat barlagy; ýokary derejeli kemçilikde build togtaýar

## P3. Infrastruktura

- [ ] Hosting saýlamak (bulut/VPS/ýerli serwer) we maglumat ýerleşýän ýurt talaplaryny barlamak
- [ ] Infrastructure as Code (Terraform/Ansible)
- [ ] Reverse proxy: Nginx/Traefik/YARP
- [ ] Dolandyrylýan ýa-da HA PostgreSQL, Redis
- [ ] CDN we obýekt saklaýyş
- [ ] E-poçta we SMS üçin production hyzmatlary
- [ ] 🔒 TLS 1.2+, awtomatik sertifikat täzelemek, HSTS preload
- [ ] 🔒 Tor bölünişi: DB, Redis, gözleg diňe içerki torda
- [ ] 🔒 Firewall: diňe 80/443 açyk; SSH diňe açar bilen, VPN/bastion arkaly
- [ ] 🔒 WAF we DDoS goragy
- [ ] 🔒 Secret manager (Vault ýa-da bulut hyzmaty) we açarlary yzygiderli çalyşmak
- [ ] 🔒 Disk we ätiýaçlyk nusgalary şifrlemek
- [ ] 🔒 Serwerleri berkitmek: awtomatik howpsuzlyk täzelenmeleri, fail2ban, artykmaç hyzmatlary öçürmek
- [ ] 🔒 Domen hasabynda 2FA, DNS üýtgeşmelerini gözegçilikde saklamak, CAA ýazgysy

## P4. Gözegçilik

- [ ] Merkezi loglar (Loki/ELK/Seq)
- [ ] Metrikalar: Prometheus + Grafana
- [ ] Trace: OpenTelemetry + Tempo/Jaeger
- [ ] Biznes metrikalary: sargyt sany, töleg şowlulygy
- [ ] SLO kesgitlemek (mysal üçin, 99.9% elýeterlilik)
- [ ] Daşyndan elýeterlilik barlagy (uptime)
- [ ] Duýduryşlar: ýalňyşlyk derejesi, gijikme, disk, DB, nobat, töleg şowsuzlygy
- [ ] 🔒 Howpsuzlyk wakalaryny merkezi ýygnamak we duýdurmak
- [ ] 🔒 Log ygtyýarlary çäkli, saklama möhleti kesgitli, şahsy maglumat ýok

## P5. Ätiýaçlyk nusgalar we dikeltmek

- [ ] RPO we RTO kesgitlemek
- [ ] PostgreSQL: awtomatik backup + PITR (WAL arhiwi)
- [ ] Faýl saklaýyşyň ätiýaçlyk nusgasy
- [ ] Hasaplaryň, sertifikatlaryň, gizlin maglumatlaryň dikeldiş prosedurasy
- [ ] Dikeltmegi yzygiderli synagdan geçirmek (azyndan her çärýekde)
- [ ] 🔒 Ätiýaçlyk nusgalar şifrlenen we başga hasapda/ýerde saklanýar (ransomware-e garşy üýtgedilmeýän)

## P6. Ýük synagy

- [ ] k6 ssenariýalary: katalog, gözleg, checkout
- [ ] Adaty, iň ýokary we aksiýa ýüki (mysal üçin, gysga möhletli arzanladyş)
- [ ] Uzak wagtlyk (soak) synag: ýat syzmalary
- [ ] Päsgelçilikleri düzetmek we kuwwat meýilnamasy
- [ ] Hyzmat näsazlygynda özüni alyp barşy: Redis, gözleg, töleg işlemese näme bolýar

## P7. Howpsuzlyk auditi we kanuny taraplar

- [ ] 🔒 OWASP ZAP (DAST) staging-de awtomatik
- [ ] 🔒 Garaşsyz hünärmen tarapyndan pentest; tapylanlary düzedip gaýtadan barlamak
- [ ] 🔒 OWASP ASVS Level 2 çek-listini tamamlamak
- [ ] 🔒 Hadysa ýüze çykanda hereket meýilnamasy: jogapkärler, aragatnaşyk, açarlary ýatyrmak, müşderileri habarly etmek
- [ ] 🔒 `security.txt` we kemçilik habar bermek tertibi
- [ ] 🔒 Işgär girişlerini yzygiderli gözden geçirmek; işden çykanda derrew ýapmak
- [ ] Ýerli kanunçylygy we şahsy maglumatlar talaplaryny hünärmen bilen barlamak
- [ ] Gizlinlik syýasaty, ulanyş şertleri, yzyna gaýtarma düzgünleri (tk, ru, en)
- [ ] Cookie razylygy
- [ ] Bank/töleg provideri bilen şertnama we talaplary

## P8. Işe girizmek

- [ ] Runbook: adaty hadysalar we olary çözmek
- [ ] Ulanyjy we admin gollanmalary
- [ ] Support kanaly we tiket prosesi
- [ ] Hakyky firma bilen synag (pilot)
- [ ] Hakyky pul bilen ähli töleg ýollaryny barlamak
- [ ] Köne ulgamdan maglumat geçirmek (bar bolsa)
- [ ] Domen, TLS, e-poçta, SEO, analitika gutarnykly barlagy
- [ ] Backup-dan dikeltmegiň production öňüsyrasynda soňky synagy
- [ ] 🔒 Dev/test hasaplaryny, başlangyç açar sözlerini we debug endpoint-lerini aýyrmak
- [ ] 🔒 P7 bendinden galan ýokary we kritiki kemçilikler ýok
- [ ] Bellenen wagtda çap etmek we ilkinji günlerde güýçlendirilen gözegçilik

## P9. Işe girizilenden soň

- [ ] Her hepde: paket täzelenmeleri, duýduryşlary gözden geçirmek
- [ ] Her aý: howpsuzlyk täzelenmeleri, haýal soraglar, çykdajylar
- [ ] Her çärýek: backup dikeltme synagy, giriş ygtyýarlaryny gözden geçirmek, açar rotasiýasy
- [ ] Her ýyl: pentest, howp modelini täzelemek, .NET/Node LTS wersiýa geçiş meýilnamasy
- [ ] Ulanyjy pikirlerini we analitikany ýygnamak, `[V2]` işlerini tertipleşdirmek
