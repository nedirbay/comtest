#!/usr/bin/env bash
# A private backend for the browser tests: its own database, mailbox and signing key, on ports of
# its own. The tests create hundreds of users and stores, and none of that may reach a real
# development database. Every password here is random and lives only in the .e2e folder.
#
#   e2e/stack.sh up     build the API, create the database and start everything
#   e2e/stack.sh down   stop everything and delete the data
set -euo pipefail

frontend="$(cd "$(dirname "$0")/.." && pwd)"
backend="$frontend/../backend"
state="$frontend/.e2e"

db=comtest-e2e-db
mail=comtest-e2e-mail
storage=comtest-e2e-storage
db_port=55432
smtp_port=51025
mail_ui_port=58025
api_port=5055
storage_port=59000

down() {
  if [[ -f "$state/api.pid" ]]; then
    kill "$(cat "$state/api.pid")" 2>/dev/null || true
  fi

  docker rm -f "$db" "$mail" "$storage" >/dev/null 2>&1 || true
  rm -rf "$state"
}

up() {
  down
  mkdir -p "$state"

  local owner_password app_password signing_key storage_key storage_secret
  storage_key="e2e$(openssl rand -hex 8)"
  storage_secret="$(openssl rand -hex 24)"
  owner_password="$(openssl rand -hex 24)"
  app_password="$(openssl rand -hex 24)"
  signing_key="$(openssl ecparam -name prime256v1 -genkey -noout | openssl pkcs8 -topk8 -nocrypt)"

  docker run -d --name "$db" -e POSTGRES_PASSWORD="$owner_password" \
    -p "127.0.0.1:$db_port:5432" postgres:16-alpine >/dev/null
  docker run -d --name "$mail" -p "127.0.0.1:$smtp_port:1025" -p "127.0.0.1:$mail_ui_port:8025" \
    axllent/mailpit:latest >/dev/null
  docker run -d --name "$storage" -e MINIO_ROOT_USER="$storage_key" -e MINIO_ROOT_PASSWORD="$storage_secret" \
    -p "127.0.0.1:$storage_port:9000" cgr.dev/chainguard/minio:latest server /data >/dev/null

  until docker exec "$db" pg_isready -q -U postgres -h 127.0.0.1; do sleep 1; done

  docker exec -i "$db" psql -q -U postgres -v ON_ERROR_STOP=1 -v app_password="$app_password" \
    <"$backend/scripts/db/create-roles.sql" >/dev/null
  docker exec "$db" psql -q -U postgres -c 'CREATE DATABASE comtest_e2e;' >/dev/null

  export ASPNETCORE_ENVIRONMENT=Development
  export ASPNETCORE_URLS="http://localhost:$api_port"
  export ConnectionStrings__Default="Host=localhost;Port=$db_port;Database=comtest_e2e;Username=comtest_app;Password=$app_password"
  export ConnectionStrings__Migrations="Host=localhost;Port=$db_port;Database=comtest_e2e;Username=postgres;Password=$owner_password"
  export Jwt__ActiveKeyId=e2e
  export Jwt__SigningKeys__0__KeyId=e2e
  export Jwt__SigningKeys__0__PrivateKeyPem="$signing_key"
  export Email__Smtp__Host=localhost
  export Email__Smtp__Port="$smtp_port"
  export Email__Smtp__UseStartTls=false
  export Auth__FrontendBaseUrl=http://localhost:3001
  export Storage__ServiceUrl="http://localhost:$storage_port"
  export Storage__AccessKey="$storage_key"
  export Storage__SecretKey="$storage_secret"
  export Storage__CreateBucketIfMissing=true
  export Auth__BreachedPasswordCheckEnabled=false
  export Cors__AllowedOrigins__0=http://localhost:3001
  export Cors__AllowedOrigins__1=http://localhost:5174
  # One browser on one address does everything here, so the per-client limits would only get in the way.
  export RateLimiting__GlobalPerMinute=100000
  export RateLimiting__LoginPerMinute=100000
  export RateLimiting__AccountActionPerMinute=100000
  export RateLimiting__RefreshPerMinute=100000
  export RateLimiting__CheckoutPerMinute=100000
  export RateLimiting__TrackingPerMinute=100000

  cd "$backend"
  dotnet tool restore >/dev/null
  dotnet build src/ComTest.Api -v quiet --nologo >"$state/build.log"
  dotnet ef database update --no-build --project src/ComTest.Infrastructure \
    --startup-project src/ComTest.Api >"$state/migrate.log"
  dotnet run --no-build --no-launch-profile --project src/ComTest.Api -- seed >"$state/seed.log"

  nohup dotnet run --no-build --no-launch-profile --project src/ComTest.Api \
    >"$state/api.log" 2>&1 &
  echo $! >"$state/api.pid"

  until curl -fs -o /dev/null "http://localhost:$api_port/health/ready"; do
    kill -0 "$(cat "$state/api.pid")" 2>/dev/null || {
      echo "The API stopped. See $state/api.log" >&2
      exit 1
    }
    sleep 1
  done

  echo "Ready: API on $api_port, mailbox on $mail_ui_port"
}

case "${1:-}" in
  up) up ;;
  down) down ;;
  *)
    echo "Usage: $0 up|down" >&2
    exit 2
    ;;
esac
