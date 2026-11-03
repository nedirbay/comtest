# E-commerce platforma — iş meýilnamasy

Dükanlar we söwda firmalary üçin döwrebap e-commerce platforma.

- **Backend:** ASP.NET Core (.NET 10)
- **Frontend:** Vue 3 + TypeScript, Element Plus, Tailwind CSS

## Bu faýly nädip ulanmaly

- `- [ ]` — edilmeli iş, `- [x]` — ýerine ýetirilen iş.
- 🔒 — howpsuzlyk işi. Bu işler soňa goýulmaýar: her biri öz tapgyrynda ýerine ýetirilýär.
- `[MVP]`, `[V1]`, `[V2]` — mümkinçiligiň haýsy wersiýa degişlidigi.
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

Bu kararlar meýilnamanyň esasyny düzýär. Işe başlamazdan öň tassyklamaly.

- [ ] **K1. Platforma modeli:** multi-tenant SaaS — bir platformada köp firma, her firmanyň öz dükany bar. Bir firma üçin hem şol bir kod işleýär.
- [ ] **K2. Arhitektura:** modular monolith + Clean Architecture. Microservice başda artykmaç çylşyrymlylyk döredýär; gerek bolanda modullar aýrylyp bilner.
- [ ] **K3. Maglumatlar bazasy:** PostgreSQL + Row-Level Security (tenant üzňeligi baza derejesinde hem goralýar).
- [ ] **K4. Keş we fon işleri:** Redis, Hangfire; gerek bolanda RabbitMQ.
- [ ] **K5. Faýl saklaýyş:** S3-e laýyk saklaýyş (dev: MinIO) + CDN.
- [ ] **K6. Storefront:** Nuxt (SSR, Vue 3 + TS). Haryt sahypalary gözleg ulgamlarynda görünmeli, arassa SPA munuň üçin ýaramaz.
- [ ] **K7. Admin panel:** Vue 3 + Vite SPA (SEO gerek däl).
- [ ] **K8. Auth:** gysga ömürli access JWT (brauzeriň RAM-ynda) + refresh token (`HttpOnly` cookie, rotation).
- [ ] **K9. Diller:** türkmen, rus, iňlis.
- [ ] **K10. Pul:** esasy pul birligi TMT, goşmaça USD. Pul mukdarlary `decimal` görnüşinde saklanýar, `float`/`double` ulanylmaýar.
- [ ] **K11. Töleg:** provider abstraksiýasy. Tertip: alanda nagt → bank geçirimi → ýerli bank ekwaýringi → halkara provider. Kart maglumatlary biziň serwerimize düşmeýär.
- [ ] **K12. Kitaphana ygtyýarnamalary:** MediatR, AutoMapper, MassTransit (v9+) we FluentAssertions (v8+) täjirçilik ygtyýarnamasyna geçdi. Olaryň ýerine öz handler-lerimiz, Mapperly, AwesomeAssertions/Shouldly ulanylýar.

---

# 1-nji bölüm. Mümkinçilikler sanawy

## 1.1. Müşderi tarapy (storefront)

**Katalog we gözleg**

- [ ] `[MVP]` Kategoriýalar agajy we haryt sanawy
- [ ] `[MVP]` Haryt sahypasy: suratlar galereýasy, düşündiriş, häsiýetnamalar, galyndy ýagdaýy
- [ ] `[MVP]` Haryt wariantlary (reňk, ölçeg, göwrüm)
- [ ] `[MVP]` Tekst boýunça gözleg
- [ ] `[MVP]` Süzgüçler (baha, marka, häsiýet) we tertipleşdirme
- [ ] `[V1]` Gözlegde awtomatik doldurma we ýalňyş ýazuwa çydamlylyk
- [ ] `[V1]` Harytlary deňeşdirmek
- [ ] `[V1]` Soňky görlen harytlar, meňzeş harytlar, „bilelikde satyn alynýar“
- [ ] `[V1]` Haryt peýda bolanda habar bermek
- [ ] `[V2]` AI esasly şahsy teklipler
- [ ] `[V2]` Surat boýunça gözleg

**Sebet we sargyt**

- [ ] `[MVP]` Sebet (myhman we hasaba alnan ulanyjy üçin, ulgama girende birleşýär)
- [ ] `[MVP]` Myhman hökmünde sargyt etmek
- [ ] `[MVP]` Sargyt resmileşdirmek: salgy, eltip bermek görnüşi, töleg görnüşi
- [ ] `[MVP]` Eltip bermegiň bahasynyň hasaplanmagy
- [ ] `[MVP]` Sargyt ýagdaýyny yzarlamak
- [ ] `[V1]` Promokod we sowgat kartasyny ulanmak
- [ ] `[V1]` Taşlanan sebet barada ýatlatma
- [ ] `[V1]` Bir basmada gaýtadan sargyt etmek
- [ ] `[V2]` Bölüp tölemek (nesiýe, kredit)
- [ ] `[V2]` Yzygiderli sargyt (abuna)

**Şahsy otag**

- [ ] `[MVP]` Hasaba alynmak, ulgama girmek (e-poçta/telefon)
- [ ] `[MVP]` Profil we salgylar kitaby
- [ ] `[MVP]` Sargytlaryň taryhy
- [ ] `[V1]` Halanlarym sanawy
- [ ] `[V1]` Haryt barada syn we baha (suratly)
- [ ] `[V1]` Yzyna gaýtarma haýyşy
- [ ] `[V1]` Bonus ballary we wepalylyk derejesi
- [ ] `[V1]` Iki basgançakly tassyklama (2FA), Google bilen girmek
- [ ] `[V1]` Şahsy maglumatlary göçürip almak we hasaby pozmak

**Umumy**

- [ ] `[MVP]` Köp dilli interfeýs (tk, ru, en)
- [ ] `[MVP]` Responsiw dizaýn (telefon, planşet, kompýuter)
- [ ] `[V1]` Köp pul birligi
- [ ] `[V1]` Gara tema
- [ ] `[V1]` PWA (telefona gurnamak, push bildirişler)
- [ ] `[V1]` Onlaýn kömek söhbetdeşligi
- [ ] `[V2]` Android/iOS programma
- [ ] `[V2]` Telegram bot arkaly sargyt

## 1.2. Dükan eýesi tarapy (admin panel)

**Harytlar we ammar**

- [ ] `[MVP]` Harytlary, kategoriýalary, markalary dolandyrmak
- [ ] `[MVP]` Wariantlar, SKU, ştrih-kod, suratlar
- [ ] `[MVP]` Ammar galyndysyny dolandyrmak, az galanda duýduryş
- [ ] `[V1]` Excel/CSV arkaly köpçülikleýin import/eksport
- [ ] `[V1]` Köp ammar we şahamça
- [ ] `[V1]` Ammar hereketleriniň taryhy, inwentarizasiýa
- [ ] `[V1]` Üpjünçiler we satyn alma sargytlary
- [ ] `[V2]` 1C/ERP bilen sinhronlaşdyrmak

**Sargytlar we müşderiler**

- [ ] `[MVP]` Sargytlary görmek, süzmek, ýagdaýyny üýtgetmek
- [ ] `[MVP]` Müşderiler sanawy we sargyt taryhy
- [ ] `[V1]` Faktura we goşma haty çap etmek (PDF)
- [ ] `[V1]` Yzyna gaýtarma we pul gaýtarma
- [ ] `[V1]` Kurýerlere sargyt bellemek
- [ ] `[V1]` Müşderi toparlary we CRM bellikleri
- [ ] `[V2]` POS (dükandaky kassa) onlaýn ammar bilen bilelikde

**Marketing**

- [ ] `[V1]` Arzanladyşlar, promokodlar, gysga möhletli aksiýalar
- [ ] `[V1]` Bannerler we baş sahypany dolandyrmak
- [ ] `[V1]` CMS sahypalary we blog
- [ ] `[V1]` SEO sazlamalary (meta, slug, sitemap)
- [ ] `[V1]` E-poçta/SMS kampaniýalary
- [ ] `[V2]` Referal programmasy
- [ ] `[V2]` AI bilen haryt düşündirişini taýýarlamak

**Dolandyryş we hasabatlar**

- [ ] `[MVP]` Dolandyryş paneli (dashboard): satuw, sargyt, galyndy
- [ ] `[MVP]` Dükan sazlamalary: logotip, aragatnaşyk, eltip bermek, töleg
- [ ] `[MVP]` Işgärler we rollar
- [ ] `[V1]` Satuw, haryt, müşderi hasabatlary we Excel eksporty
- [ ] `[V1]` Audit žurnaly (kim, haçan, näme üýtgetdi)
- [ ] `[V1]` Salgyt sazlamalary
- [ ] `[V1]` Dükanyň öz domeni we tema sazlamalary
- [ ] `[V2]` Girdeji we gazanç seljermesi, isleg çaklamasy

## 1.3. Platforma tarapy (super admin)

- [ ] `[V1]` Firmalary hasaba almak, tassyklamak, wagtlaýyn togtatmak
- [ ] `[V1]` Tarif meýilnamalary we çäklendirmeler
- [ ] `[V1]` Abuna tölegleri we hasaplar
- [ ] `[V1]` Platforma statistikasy we gözegçilik
- [ ] `[V2]` Marketplace: bir sahypada köp satyjy, komissiýa, satyjy tölegleri

## 1.4. Integrasiýalar

- [ ] `[MVP]` E-poçta
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
- [ ] Node.js-i häzirki LTS wersiýa täzelemek (gurlan v18.19.1-iň goldawy gutardy, Vite-iň täze wersiýalary onda işlemeýär)
- [ ] `corepack` arkaly pnpm gurmak
- [ ] `git init` we uzakdaky repozitoriý döretmek (häzir bukja git repozitoriýasy däl)
- [ ] Bukja gurluşyny döretmek: `backend/`, `frontend/`, `deploy/`, `docs/`
- [ ] `.gitignore`, `.editorconfig`, `.gitattributes`, `README.md` goşmak
- [ ] Şaha strategiýasyny we Conventional Commits kadasyny kesgitlemek
- [ ] PR şablonyny we „tamamlandy“ kesgitlemesini ýazmak
- [ ] `docker-compose.yml`: PostgreSQL, Redis, MinIO, Mailpit, Seq
- [ ] Domen modelini we ER diagrammasyny taýýarlamak
- [ ] Arhitektura kararlaryny ADR görnüşinde `docs/adr/` bukjasyna ýazmak
- [ ] 🔒 `main` şahasyny goramak: hökmany PR we CI, göni push gadagan
- [ ] 🔒 Git hesaplarynda 2FA-ny hökmany etmek
- [ ] 🔒 `gitleaks` pre-commit hook we CI barlagy (gizlin maglumatlaryň kod bilen düşmezligi üçin)
- [ ] 🔒 `.env.example` döretmek; hakyky `.env` faýllary hiç haçan commit edilmeýär
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
- [ ] 🔒 Resource esasly barlag: başga ulanyjynyň sargydyna/salgysyna giriş (IDOR) gadagan
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
- [ ] 🔒 Keş açarlaryna, fon işlerine, faýl ýollaryna we gözleg indeksine `TenantId` goşmak
- [x] 🔒 `IgnoreQueryFilters` ulanylyşyny analizator/kod syny bilen gözegçilikde saklamak
- [ ] 🔒 Integrasiýa testi: iki tenant arasynda maglumat syzmasynyň ýoklugyny her endpoint üçin barlamak

## B5. Katalog

- [x] `Category` (agaç gurluşy), `Brand`, `Product`, `ProductVariant`, `Attribute`
- [x] Haryt CRUD: garalama/çap edilen/arhiw ýagdaýlary
- [x] Köp dilli at, düşündiriş, slug
- [x] Baha, köne baha, özüne düşýän baha, salgyt topary
- [x] Kategoriýa boýunça dinamiki häsiýetler
- [x] Sanaw: sahypalama, süzgüç, tertipleşdirme
- [x] Baha üýtgemeleriniň taryhy
- [x] Storefront üçin aýratyn read modeli we keş
- [ ] `[V1]` Excel/CSV importy (fon işi, ýalňyşlyk hasabaty bilen)
- [x] 🔒 Request/response üçin aýratyn DTO (mass assignment we artykmaç maglumat öňüni almak)
- [x] 🔒 Özüne düşýän baha ýaly içerki meýdanlar açyk API-de görkezilmeýär
- [x] 🔒 Tertipleşdirme meýdanlary üçin allowlist; sahypa ululygynda ýokarky çäk
- [x] 🔒 HTML düşündirişi serwerde arassalamak (`HtmlSanitizer`)

## B6. Faýllar we suratlar

- [ ] `IFileStorage` abstraksiýasy (S3/MinIO)
- [ ] Presigned URL bilen ýüklemek
- [ ] Suratlary WebP/AVIF formatyna we dürli ululyklara öwürmek (fon işi)
- [ ] CDN we uzak möhletli keş sözbaşylary
- [ ] Ulanylmaýan faýllary arassalamak işi
- [ ] 🔒 Faýl görnüşini giňeltme boýunça däl, „magic bytes“ boýunça barlamak (allowlist)
- [ ] 🔒 Faýl ululygyna çäk, tenant boýunça ýer kwotasy
- [ ] 🔒 Suratlary täzeden kodlamak we EXIF maglumatlaryny aýyrmak
- [ ] 🔒 Tötänleýin faýl atlary; path traversal öňüni almak
- [ ] 🔒 SVG ýüklemek gadagan ýa-da arassalanan; goşmaça domenden bermek
- [ ] 🔒 Bucket ýapyk; diňe CDN/presigned URL arkaly giriş
- [ ] 🔒 `[V1]` ClamAV bilen wirus barlagy

## B7. Ammar

- [ ] `Warehouse`, `StockItem`, `StockMovement`
- [ ] Galyndy hereketleri: girdeji, çykdajy, düzediş, rezerw
- [ ] Sargyt üçin rezerw (TTL bilen) we möhleti geçenini boşatmak
- [ ] Az galan haryt duýduryşy
- [ ] `[V1]` Köp ammar, ammarlar arasynda geçirim
- [ ] `[V1]` Inwentarizasiýa
- [ ] `[V1]` Üpjünçiler we satyn alma sargytlary
- [ ] 🔒 Galyndy üýtgemeleriniň ählisi kim tarapyndan edilendigi bilen ýazga alynýar
- [ ] Test: bir wagtda köp sargytda galyndynyň minus bolmazlygy (race condition)

## B8. Sebet we checkout

- [ ] Myhman sebedi (gizlin kukili ID) we ulanyjy sebedi; ulgama girende birleşdirmek
- [ ] Sebetdäki baha/galyndy üýtgemelerini barlamak we duýdurmak
- [ ] Checkout hasaplamasy: harytlar, arzanladyş, salgyt, eltip bermek
- [ ] Salgy we eltip bermek görnüşini saýlamak
- [ ] 🔒 Baha, arzanladyş we jemi diňe serwerde hasaplanýar; müşderiden gelen baha kabul edilmeýär
- [ ] 🔒 Mukdar üçin walidasiýa: otrisatel, nol, aşa uly bahalar
- [ ] 🔒 Sargyt döretmekde idempotency key (iki gezek basylanda iki sargyt bolmaz)
- [ ] 🔒 Sargyt spamynyň öňüni almak: rate limit, bot goragy, myhman üçin telefon tassyklamak

## B9. Sargytlar

- [ ] `Order`, `OrderItem`, ýagdaý taryhy
- [ ] Ýagdaý maşyny: `Pending → Confirmed → Packed → Shipped → Delivered`, `Cancelled`, `Returned`
- [ ] Harytlaryň at we baha nusgasyny (snapshot) sargytda saklamak
- [ ] Ýatyrylanda galyndyny yzyna goýmak
- [ ] Admin üçin sanaw, süzgüç, jikme-jiklik, bellikler
- [ ] Domen wakalary: `OrderCreated`, `OrderPaid`, `OrderShipped`
- [ ] `[V1]` Faktura we goşma haty (PDF; QuestPDF ygtyýarnamasyny barlamak)
- [ ] `[V1]` Yzyna gaýtarma (RMA) akymy
- [ ] 🔒 Sargyt belgisi yzygiderli we çaklap bolýan däl
- [ ] 🔒 Myhman sargydyny yzarlamak gollanan token arkaly
- [ ] 🔒 Ýagdaý geçişleri rol boýunça ygtyýarlandyrylýar we audit edilýär

## B10. Tölegler

**Nagt (MVP)**

- [ ] `IPaymentProvider` abstraksiýasy we `Payment`, `Refund` modelleri
- [ ] Alanda nagt tölemek
- [ ] Bank geçirimi: el bilen tassyklamak, audit bilen

**Onlaýn (V1)**

- [ ] Ýerli bank ekwaýringini birikdirmek (redirect/hosted sahypa)
- [ ] Doly we bölekleýin pul gaýtarma
- [ ] Tölegleri bank hasabaty bilen deňeşdirmek (reconciliation, her gün)
- [ ] Tamamlanmadyk tölegleriň ýagdaýyny barlamak we möhleti geçenleri ýatyrmak
- [ ] `[V2]` Halkara provider

**Howpsuzlyk**

- [ ] 🔒 Kart maglumatlary serwere düşmeýär we saklanmaýar (hosted sahypa, PCI DSS SAQ A derejesi)
- [ ] 🔒 Webhook goly, wagt belgisi we gaýtalanmaga garşy barlag
- [ ] 🔒 Sargyt diňe serwer tarapyndan tassyklanandan soň tölenen bolýar; brauzeriň gaýdyp gelmegine ynanylmaýar
- [ ] 🔒 Gelen mukdar we pul birligi sargyt bilen deňeşdirilýär
- [ ] 🔒 Ähli töleg amallarynda idempotency; iki gezek tutulmagyň öňüni almak
- [ ] 🔒 Pul gaýtarma aýratyn ygtyýar talap edýär; uly mukdarlar üçin ikinji tassyk
- [ ] 🔒 Töleg ýagdaý üýtgemeleri üýtgedip bolmaýan žurnala ýazylýar
- [ ] 🔒 Bank açarlary diňe secret manager-de

## B11. Eltip bermek

- [ ] Eltip bermek zolaklary (welaýat, etrap, şäher) we tarifler
- [ ] Mugt eltip bermegiň çägi
- [ ] Dükandan özi alyp gitmek
- [ ] Sargyt yzarlaýyş maglumaty
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
- [ ] E-poçta (MailKit, fon nobaty, gaýtalamak)
- [ ] SMS provider abstraksiýasy
- [ ] SignalR: admin üçin täze sargyt bildirişi, müşderi üçin ýagdaý üýtgemesi
- [ ] `[V1]` Telegram bildirişleri, web push
- [ ] `[V1]` Bildiriş sazlamalary we ýazylmadan çykmak
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
- [ ] Satuw, haryt, müşderi, ammar hasabatlary
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

- [ ] Unit testler: domen we biznes kadalary (xUnit)
- [ ] Integrasiýa testleri: Testcontainers bilen hakyky PostgreSQL we Redis
- [x] API testleri: `WebApplicationFactory`
- [ ] Doly ssenariý testi: hasaba alyş → sebet → sargyt → töleg → eltip bermek
- [ ] Race condition testleri: galyndy, promokod, töleg
- [ ] OpenAPI kontrakt barlagy
- [ ] CI-da örtük hasabaty we iň az çäk (biznes logikasy üçin ≥ 80%)
- [ ] 🔒 Ygtyýarlandyrma matrisasy testi: her endpoint × her rol
- [x] 🔒 Her endpoint-iň ygtyýar talap edýändigini awtomatik barlamak
- [x] 🔒 Tenant üzňeligi test toplumy
- [ ] 🔒 Regressiýa testi: tapylan her howpsuzlyk kemçiligi üçin

## B20. Howpsuzlyk gutarnykly barlagy

- [ ] 🔒 `dotnet list package --vulnerable --include-transitive` CI-da
- [ ] 🔒 Dependabot/Renovate bilen paketleri täzelemek
- [ ] 🔒 CodeQL (SAST) CI-da
- [ ] 🔒 Şahsy maglumatlaryň sütün derejesinde şifrlenmegi (zerur ýerlerde)
- [ ] 🔒 Maglumat saklama möhletleri we awtomatik arassalamak
- [ ] 🔒 Audit žurnaly üýtgedip bolmaýan görnüşde saklanýar
- [ ] 🔒 Şübheli hereketlere duýduryş: köp şowsuz giriş, tenant bozulma synanyşygy, töleg ýalňyşlyklary
- [ ] 🔒 Open redirect: `returnUrl` üçin allowlist
- [ ] 🔒 Biznes logikasyna hüjümleri barlamak: promokod, bonus, yzyna gaýtarma, mukdar manipulýasiýasy
- [ ] 🔒 OWASP API Security Top 10 boýunça her bent barlanýar
- [ ] 🔒 OWASP ASVS Level 2 boýunça çek-list doldurulýar

---

# 4-nji bölüm. Frontend (Vue 3, TypeScript, Element Plus, Tailwind CSS)

## F0. Taslama gurluşy

- [ ] pnpm workspace monorepo: `apps/storefront`, `apps/admin`, `packages/ui`, `packages/api-client`, `packages/config`
- [ ] `apps/admin`: Vite + Vue 3 + TS
- [ ] `apps/storefront`: Nuxt + TS (K6 kararyna görä)
- [ ] `strict` TypeScript, `vue-tsc` barlagy
- [ ] ESLint (flat config), Prettier
- [ ] Husky, lint-staged, commitlint
- [ ] Feature esasly bukja gurluşy
- [ ] Elementleri zerur bolanda ýüklemek: `unplugin-vue-components` + `ElementPlusResolver`
- [ ] 🔒 `pnpm-lock.yaml` commit edilýär; CI-da `--frozen-lockfile`
- [ ] 🔒 Paketleriň install skriptleri diňe rugsat berlen sanaw boýunça

## F1. Dizaýn ulgamy

- [ ] Dizaýn tokenleri: reňk, aralyk, şrift, radius, kölege
- [ ] Tailwind sazlamak we tokenleri Element Plus CSS üýtgeýjileri bilen birleşdirmek
- [ ] Tailwind preflight bilen Element Plus stilleriniň çaknyşmagynyň öňüni almak (CSS `@layer` tertibi)
- [ ] Element Plus temasy we dil faýllary (tk üçin öz terjimämiz, ru, en)
- [ ] Gara tema
- [ ] Umumy elementler: `AppButton`, `AppTable`, `AppForm`, `AppDialog`, `PageHeader`
- [ ] Boş, ýüklenýän (skeleton) we ýalňyşlyk ýagdaýlary
- [ ] Pul, sene, telefon formatlaýjylary
- [ ] Responsiw sazlamalar we mobile-first kadalary
- [ ] Storybook/Histoire bilen elementleri resminamalaşdyrmak

## F2. Esasy gatlaklar

- [ ] OpenAPI-den TypeScript tiplerini we API müşderisini awtomatik döretmek
- [ ] HTTP gatlagy: bazalaýyn URL, `Accept-Language`, `CorrelationId`, ýalňyşlyklary işlemek
- [ ] TanStack Query: server maglumatlary, keş, täzeden synanyşmak
- [ ] Pinia: diňe müşderi ýagdaýy (auth, sebet, UI)
- [ ] Vue Router: lazy loading, layout-lar, route meta
- [ ] vue-i18n: tk, ru, en; dil URL-de saklanýar
- [ ] Zod bilen forma walidasiýasy; serwer ýalňyşlyklaryny meýdanlara baglamak
- [ ] Global ýalňyşlyk işleýjisi, Error Boundary, 404/403/500 sahypalary
- [ ] Sentry bilen ýalňyşlyklary yzarlamak
- [ ] 🔒 Access token diňe RAM-da; `localStorage`/`sessionStorage`-da token saklamak gadagan
- [ ] 🔒 Silent refresh: bir wagtda diňe bir refresh sorag (single-flight)
- [ ] 🔒 Cookie diňe öz API domenine iberilýär (`withCredentials` çäkli)
- [ ] 🔒 `VITE_*`/`NUXT_PUBLIC_*` üýtgeýjilerinde gizlin maglumat ýok (bular brauzerde görünýär)
- [ ] 🔒 Route guard diňe UX üçin; hakyky barlag serwerde
- [ ] 🔒 Sentry-de şahsy maglumatlary we tokenleri arassalamak

## F3. Autentifikasiýa sahypalary

- [ ] Hasaba alynmak, ulgama girmek, ulgamdan çykmak
- [ ] E-poçta/telefon tassyklamak (OTP girizmek elementi)
- [ ] Açar sözüni dikeltmek
- [ ] Admin üçin 2FA gurnamak we girmek, dikeldiş kodlary
- [ ] Aktiw sessiýalar sahypasy
- [ ] Rol/permission esasly görkeziş: `v-can` direktiwasy
- [ ] 🔒 Giriş ýalňyşlygynda umumy habar (ulanyjy bar/ýok açyk aýdylmaýar)
- [ ] 🔒 `returnUrl`-y diňe içerki ýollara rugsat bermek (open redirect)
- [ ] 🔒 Ulgamdan çykanda ähli ýagdaý, keş we query cache arassalanýar
- [ ] 🔒 `autocomplete` atributlary dogry; açar sözi dolandyryjylary bilen sazlaşykly

## F4. Storefront: katalog

- [ ] Layout: header, menýu, gözleg, sebet, dil saýlamak, footer
- [ ] Baş sahypa: bannerler, kategoriýalar, täze we köp satylýan harytlar
- [ ] Kategoriýa sahypasy: süzgüçler, tertipleşdirme, sahypalama
- [ ] Süzgüç ýagdaýy URL-de saklanýar
- [ ] Haryt kartasy
- [ ] Haryt sahypasy: galereýa, wariant saýlamak, häsiýetler, synlar
- [ ] Gözleg sahypasy we awtomatik doldurma
- [ ] Telefon üçin süzgüç paneli we aşaky nawigasiýa
- [ ] `[V1]` Deňeşdirmek, soňky görlenler, meňzeş harytlar
- [ ] 🔒 CMS/düşündiriş HTML-i diňe DOMPurify-dan soň görkezilýär
- [ ] 🔒 SSR: ulanyjy maglumatlary soraglar arasynda garyşmaýar (global ýagdaý ýok, Pinia her sorag üçin aýratyn)
- [ ] 🔒 SSR payload-ynda token ýa-da içerki maglumat ýok

## F5. Storefront: sebet we checkout

- [ ] Sebet sahypasy we kiçi sebet (drawer)
- [ ] Myhman sebedini ulanyjy sebedi bilen birleşdirmek
- [ ] Baha/galyndy üýtgände duýduryş
- [ ] Checkout ädimleri: aragatnaşyk → salgy → eltip bermek → töleg → tassyklama
- [ ] Salgy saýlamak: welaýat, etrap, şäher
- [ ] Töleg sahypasy we bankdan gaýdyp gelmek
- [ ] Sargyt tassyklandy sahypasy
- [ ] Sargyt düwmesini iki gezek basmagyň öňüni almak
- [ ] 🔒 Ekranda görkezilýän jemi diňe serwer jogabyndan alynýar
- [ ] 🔒 Töleg sahypalarynda üçünji tarap skriptleri ýok
- [ ] 🔒 Sargyt tassyklamasy serwerden barlanýar; URL parametrine ynanylmaýar

## F6. Storefront: şahsy otag

- [ ] Profil we salgylar
- [ ] Sargytlar we yzarlaýyş
- [ ] Hakyky wagtda ýagdaý täzelenmesi (SignalR)
- [ ] `[V1]` Halanlarym, synlar, bonus ballary, yzyna gaýtarma
- [ ] `[V1]` Bildiriş sazlamalary
- [ ] `[V1]` Maglumatlary göçürip almak we hasaby pozmak
- [ ] 🔒 Telefon, e-poçta ýa-da açar sözi üýtgedilende täzeden tassyklamak

## F7. Admin: esas

- [ ] Layout: gapdal menýu, header, breadcrumbs, dükan saýlamak
- [ ] Rol boýunça menýu we sahypalar
- [ ] Umumy maglumat tablisasy: server tarapynda sahypalama, süzgüç, sütün saýlamak
- [ ] Uly sanawlar üçin `el-table-v2`
- [ ] Umumy forma drawer/dialog nagşy
- [ ] Dashboard: görkezijiler, ECharts grafikleri
- [ ] Global gözleg, klawiatura gysga ýollary
- [ ] Hakyky wagtda täze sargyt bildirişi
- [ ] 🔒 Admin panel aýratyn domende; gözleg ulgamlaryndan ýapyk (`noindex`)
- [ ] 🔒 Hereketsiz sessiýanyň awtomatik gutarmagy

## F8. Admin: harytlar we ammar

- [ ] Haryt sanawy we köpçülikleýin hereketler
- [ ] Haryt formasy: esasy maglumat, köp dilli meýdanlar, wariantlar, suratlar, SEO
- [ ] Surat ýüklemek: süýräp goýmak, tertip, ýüklenme görkezijisi
- [ ] Formatlanan tekst redaktory (TipTap)
- [ ] Kategoriýa agajy (süýräp tertipleşdirmek), markalar, häsiýetler
- [ ] Ammar galyndysy, hereketler taryhy
- [ ] Saklanmadyk üýtgeşmeler barada duýduryş
- [ ] `[V1]` Excel importy: mysal faýl, öňünden görkezmek, ýalňyşlyk hasabaty
- [ ] `[V1]` Inwentarizasiýa, üpjünçiler
- [ ] 🔒 Redaktorda rugsat berlen teglere çäk goýmak
- [ ] 🔒 Faýl ululygy/görnüşi müşderide hem barlanýar (esasy barlag serwerde)

## F9. Admin: sargytlar, müşderiler, sazlamalar

- [ ] Sargyt sanawy: ýagdaý boýunça süzgüç
- [ ] Sargyt jikme-jikligi: ýagdaý üýtgetmek, taryh, bellikler
- [ ] Müşderiler sanawy we profil
- [ ] Dükan sazlamalary: umumy, töleg, eltip bermek, dil
- [ ] Işgärler we rollar: çakylyk, permission matrisasy
- [ ] `[V1]` Faktura çap etmek, yzyna gaýtarma, pul gaýtarma
- [ ] 🔒 Howply hereketler (pozmak, pul gaýtarma) tassyklama talap edýär
- [ ] 🔒 Müşderiniň şahsy maglumatlary diňe ygtyýary bar işgärlere doly görkezilýär

## F10. Admin: marketing we mazmun

- [ ] Promokodlar, aksiýalar
- [ ] Banner, baş sahypa, menýu dolandyrmak
- [ ] CMS sahypalary, blog
- [ ] Syn moderasiýasy
- [ ] SEO sazlamalary
- [ ] E-poçta/SMS kampaniýalary

## F11. Admin: hasabatlar we audit

- [ ] Satuw, haryt, müşderi, ammar hasabatlary
- [ ] Sene aralygy saýlamak, deňeşdirme, Excel eksporty
- [ ] Audit žurnaly sahypasy
- [ ] API açarlary we webhook sazlamalary
- [ ] 🔒 Gizlin açar diňe döredilende bir gezek görkezilýär

## F12. Platforma admin paneli

- [ ] Firmalar: tassyklamak, togtatmak, tarifler
- [ ] Abuna tölegleri
- [ ] Platforma statistikasy we ulgam ýagdaýy
- [ ] Global sazlamalar
- [ ] 🔒 Aýratyn domen, hökmany 2FA, mümkin bolsa IP/VPN çäklendirmesi
- [ ] 🔒 Tenant adyndan girmek (impersonation) aýdyň görkezilýär, wagty çäkli we audit edilýär

## F13. SEO, öndürijilik, PWA

- [ ] Her sahypa üçin meta, Open Graph, canonical
- [ ] `hreflang` (tk, ru, en)
- [ ] Schema.org: `Product`, `BreadcrumbList`, `Organization`
- [ ] `sitemap.xml`, `robots.txt`
- [ ] Sahypalar boýunça kod bölmek, bundle analizi we býujeti
- [ ] Suratlar: `srcset`, WebP/AVIF, lazy loading, ölçegler (CLS üçin)
- [ ] Core Web Vitals: LCP < 2.5 s, INP < 200 ms, CLS < 0.1
- [ ] Lighthouse CI
- [ ] Haýal internet üçin synag
- [ ] `[V1]` PWA: `vite-plugin-pwa`/`@vite-pwa/nuxt`, offline sahypa, push bildirişler
- [ ] 🔒 Service worker şahsy API jogaplaryny keşlemeýär
- [ ] 🔒 Analitika we pixel-ler diňe razylykdan soň; şahsy maglumat iberilmeýär

## F14. Testler we elýeterlilik

- [ ] Vitest + Vue Test Utils: composable, store, element testleri
- [ ] API-ni MSW bilen simulirlemek
- [ ] Playwright E2E: satyn alyş akymy, admin akymlary
- [ ] Wizual regressiýa testi (esasy sahypalar)
- [ ] Brauzerler we enjamlar boýunça synag
- [ ] Klawiatura bilen doly dolandyrmak, fokus tertibi
- [ ] Reňk kontrasty, `aria` atributlary, WCAG 2.2 AA
- [ ] `axe-core` testleri
- [ ] Ähli terjimeleriň doly bolmagyny CI-da barlamak

## F15. Frontend howpsuzlygy

- [ ] 🔒 ESLint `vue/no-v-html` düzgüni; kadadan çykmalar diňe DOMPurify bilen
- [ ] 🔒 Ulanyjy URL-leri üçin protokol barlagy (`javascript:` gadagan)
- [ ] 🔒 Berk CSP: `script-src` nonce/hash, `object-src 'none'`, `frame-ancestors`, `base-uri`
- [ ] 🔒 Element Plus-yň inline stilleri üçin `style-src` sazlamasyny synagdan geçirmek; ilki `Content-Security-Policy-Report-Only`
- [ ] 🔒 CSP bozulma hasabatlaryny ýygnamak
- [ ] 🔒 Üçünji tarap skriptlerini iň az derejede saklamak; CDN-den alynanlara SRI
- [ ] 🔒 Source map-ler açyk çap edilmeýär (diňe Sentry-e)
- [ ] 🔒 `pnpm audit` we Dependabot/Renovate CI-da
- [ ] 🔒 `localStorage`-da şahsy maglumat ýok
- [ ] 🔒 Kod syny çek-listi: XSS, CSRF, açyk maglumat

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
