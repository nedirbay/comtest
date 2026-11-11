# Both frontends, one target each:
#   docker build -t comtest-storefront --target storefront frontend
#   docker build -t comtest-admin --target admin --build-arg VITE_API_URL=https://api.example.tm frontend
# The admin panel's settings are fixed at build time. The storefront's come from NUXT_* at start.
FROM node:24-slim@sha256:c86ecead9e55a5210ac1727de38a4ca840bf03dcaf34dc9749addbf383b1920c AS base
WORKDIR /src
RUN corepack enable
COPY . .
# HUSKY=0: there is no git here for the hooks to go into.
RUN HUSKY=0 pnpm install --frozen-lockfile

FROM base AS storefront-build
RUN pnpm --filter @comtest/storefront build

FROM base AS admin-build
ARG VITE_API_URL
ARG VITE_SENTRY_DSN=
ARG VITE_CSP_EXTRA_ORIGINS=
RUN test -n "$VITE_API_URL" && pnpm --filter @comtest/admin build

FROM gcr.io/distroless/nodejs24-debian13:nonroot@sha256:088a01637f22908632ba09018be2e0b516f2fa6f7869c191d29491139db43c78 AS storefront
WORKDIR /app
COPY --from=storefront-build /src/apps/storefront/.output ./
ENV NODE_ENV=production PORT=3000 NUXT_CSP_MODE=enforce
EXPOSE 3000
CMD ["server/index.mjs"]

FROM nginxinc/nginx-unprivileged:alpine@sha256:b5047b9b76845eac124358373c6852377c52e4b05834a47647e955dd91cb8f1e AS admin
# The image is rebuilt less often than Alpine fixes its packages. Trivy in the CI checks the result.
USER root
RUN apk upgrade --no-cache
USER 101
COPY apps/admin/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=admin-build /src/apps/admin/dist /usr/share/nginx/html
EXPOSE 8080
