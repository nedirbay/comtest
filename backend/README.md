# ComTest backend

Multi-tenant e-commerce API: ASP.NET Core (.NET 10), PostgreSQL.

| Taslama | Ýoly | Wezipesi |
|---|---|---|
| Api | `src/ComTest.Api` | HTTP, sazlamalar, ygtyýarlandyrma |
| Application | `src/ComTest.Application` | Ulanylyş ýagdaýlary (command/query), kontraktlar |
| Domain | `src/ComTest.Domain` | Biznes kadalary (`Modules/*`) |
| Infrastructure | `src/ComTest.Infrastructure` | EF Core, Identity, e-poçta |
| Shared | `src/ComTest.Shared` | `Result`, `Money`, bazalaýyn entity-ler |
| UnitTests | `tests/ComTest.UnitTests` | Unit, arhitektura we çeşme kodunyň kadalary |
| IntegrationTests | `tests/ComTest.IntegrationTests` | Hakyky PostgreSQL bilen (Docker gerek) |

Baglanyşyk ugry: `Api → Infrastructure → Application → Domain → Shared`. Bir modul beýlekisine diňe onuň `Contracts` namespace-i arkaly baglanyp bilýär. Muny `ArchitectureTests` barlaýar.

## Ilkinji işe girizmek

Gizlin maglumatlar repozitoriýada saklanmaýar, her kompýuterde `user-secrets`-e bir gezek ýazylýar.

```bash
cd backend
dotnet tool restore

# 1. Programma roly (superuser bilen, serwere bir gezek)
APP_PASSWORD=$(openssl rand -hex 24)
psql -h localhost -U postgres -v app_password="$APP_PASSWORD" -f scripts/db/create-roles.sql
psql -h localhost -U postgres -c 'CREATE DATABASE comtest;'

# 2. Birikmeler: Default programma üçin, Migrations diňe migrasiýa üçin
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=comtest;Username=comtest_app;Password=$APP_PASSWORD" --project src/ComTest.Api
dotnet user-secrets set "ConnectionStrings:Migrations" "Host=localhost;Port=5432;Database=comtest;Username=postgres;Password=<açar sözi>" --project src/ComTest.Api

# 3. JWT açary (ES256)
KEY=$(openssl ecparam -name prime256v1 -genkey -noout | openssl pkcs8 -topk8 -nocrypt)
dotnet user-secrets set "Jwt:ActiveKeyId" "dev-1" --project src/ComTest.Api
dotnet user-secrets set "Jwt:SigningKeys:0:KeyId" "dev-1" --project src/ComTest.Api
dotnet user-secrets set "Jwt:SigningKeys:0:PrivateKeyPem" "$KEY" --project src/ComTest.Api

# 4. Shema we demo maglumatlar
dotnet ef database update --project src/ComTest.Infrastructure --startup-project src/ComTest.Api
dotnet run --project src/ComTest.Api -- seed
```

`seed` demo dükany (harytlar, ammar, eltip bermek) döredýär we açar sözlerini bir gezek görkezýär.

### Goşmaça hyzmatlar

Surat ýüklemek üçin S3-e laýyk saklaýyş gerek. Saklaýyş sazlanmasa, API işleýär, diňe surat ýüklemek 503 gaýtarýar.

```bash
cp ../deploy/.env.example ../deploy/.env   # öz bahalaryňyzy ýazyň; 9000 porty başyboş däl bolsa, MINIO_PORT-y üýtgediň
docker compose -f ../deploy/docker-compose.yml up -d minio

dotnet user-secrets set "Storage:ServiceUrl" "http://localhost:9000" --project src/ComTest.Api
dotnet user-secrets set "Storage:AccessKey" "<MINIO_ROOT_USER>" --project src/ComTest.Api
dotnet user-secrets set "Storage:SecretKey" "<MINIO_ROOT_PASSWORD>" --project src/ComTest.Api
dotnet user-secrets set "Storage:CreateBucketIfMissing" "true" --project src/ComTest.Api
```

## Buýruklar

```bash
dotnet run --project src/ComTest.Api    # http://localhost:5050
dotnet test                             # ähli testler (Docker gerek)
dotnet test tests/ComTest.UnitTests     # diňe unit testler
dotnet format --verify-no-changes       # kod stili
```

Development-de `X-Tenant: demo` sözbaşysy dükany saýlaýar. Production-da dükan subdomen boýunça kesgitlenýär: `{slug}.{Tenancy:BaseDomain}`.

## Migrasiýalar

Programma özüni migrirlemeýär. Migrasiýa aýratyn ädimde, `Migrations` birikmesi bilen işledilýär.

```bash
# Täze migrasiýa
dotnet ef migrations add <At> --project src/ComTest.Infrastructure --startup-project src/ComTest.Api --output-dir Persistence/Migrations

# Ösdürmekde
dotnet ef database update --project src/ComTest.Infrastructure --startup-project src/ComTest.Api

# Production: synlap bolýan SQL skript
dotnet ef migrations script --idempotent --project src/ComTest.Infrastructure --startup-project src/ComTest.Api -o migrate.sql
```

`TenantId` sütunly täze tablisa üçin migrasiýada `EnableTenantRowLevelSecurity("tablisa")` hökmany. Muny `EveryTableWithATenantColumn_HasForcedRowLevelSecurity` testi barlaýar.

## Production sazlamalary

Programma aşakdakylar bolmasa işe başlamaýar:

| Sazlama | Bellik |
|---|---|
| `ConnectionStrings__Default` | `SSL Mode=VerifyFull` we superuser däl rol |
| `Jwt__*` | P-256 açar. Rotasiýa: täze açary goş, `ActiveKeyId`-ni çalyş, köne açary 15 minutdan soň aýyr |
| `DataProtection__CertificatePfxBase64` we `DataProtection__CertificatePassword` | Açarlary bazada şifrleýär |
| `Email__Smtp__Host` | Development-den başga ýerde hökmany |
| `Storage__ServiceUrl`, `Storage__AccessKey`, `Storage__SecretKey`, `Storage__Bucket` | Bucket ýapyk bolmaly. Brauzerden ýükleme üçin bucket-da admin domenine CORS `PUT` gerek |
| `Storage__PublicBaseUrl` | CDN salgysy. Bolmasa, suratlar 1 sagatlyk gollanan URL bilen berilýär |
| `Auth__FrontendBaseUrl`, `Tenancy__BaseDomain`, `Cors__AllowedOrigins__0` | Gurşawa görä |
| `ForwardedHeaders__KnownProxies__0` | Proxy arkasynda hökmany, ýogsam müşderi IP-si görünmeýär |

Docker suraty üçin ICU gerek: türkmen dili (`tk`) `InvariantGlobalization` bilen işlemeýär.

## Endpoint-ler

| Ýol | Maksady |
|---|---|
| `/health/live`, `/health/ready` | Programma we baza ýagdaýy |
| `/scalar`, `/openapi/v1.json` | API dokumentasiýasy (diňe Development) |
| `/api/v1/auth/*` | Hasaba alyş, giriş, refresh, açar sözüni dikeltmek |
| `/api/v1/account/*` | Profil, sessiýalar, 2FA, telefon |
| `/api/v1/tenants`, `/api/v1/store` | Dükan döretmek, dükanyň açyk maglumatlary |
| `/api/v1/catalog/*` | Storefront katalogy (açyk) |
| `/api/v1/cart`, `/api/v1/checkout` | Sebet we sargyt (myhman üçin hem) |
| `/api/v1/orders/track` | Myhman sargydyny gollanan token bilen yzarlamak |
| `/api/v1/account/orders`, `/api/v1/account/addresses` | Müşderiniň öz sargytlary we salgylary |
| `/api/v1/admin/*` | Dükan dolandyryşy: katalog, ammar, sargyt, töleg, eltip bermek, surat (ygtyýar we 2FA gerek) |
| `/api/v1/platform/*` | Platforma dolandyryşy |
