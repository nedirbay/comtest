# The API, and a separate image holding only the migrations:
#   docker build -t comtest-api backend
#   docker build -t comtest-migrate --target migrate backend
# Base images are pinned by digest, as a tag can be moved to point at other content.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props dotnet-tools.json .editorconfig ./
COPY src ./src
RUN dotnet restore src/ComTest.Api --locked-mode
RUN dotnet publish src/ComTest.Api --no-restore -c Release -o /publish
# The publish carries native libraries for every platform, and this image only ever runs on one.
RUN find /publish/runtimes -mindepth 1 -maxdepth 1 ! -name linux-x64 ! -name linux ! -name unix -exec rm -rf {} +

FROM build AS bundle
RUN dotnet tool restore
# Building needs a connection string to exist, and does not connect. The real one comes at run time.
RUN ConnectionStrings__Migrations="Host=unused" dotnet ef migrations bundle \
    --project src/ComTest.Infrastructure --startup-project src/ComTest.Api \
    --self-contained -r linux-x64 -o /bundle/efbundle

# The application never migrates itself: it has no rights to. This runs first, with the owner role:
#   docker run -e ConnectionStrings__Migrations="$MIGRATIONS_CONNECTION" comtest-migrate
# "extra" brings ICU, as Turkmen does not work under InvariantGlobalization.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:9b755ea799ae649141a073e30d8b62d8095a5465af2b7694d17866e8c05131d8 AS migrate
WORKDIR /app
COPY --from=bundle /bundle ./
# The bundle unpacks itself on start, and /tmp is the only place it may write.
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:9b755ea799ae649141a073e30d8b62d8095a5465af2b7694d17866e8c05131d8 AS api
WORKDIR /app
COPY --from=build /publish ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ComTest.Api.dll"]
