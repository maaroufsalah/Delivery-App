# Image du web Speedy (`frontend/`, copié sans modification) branché sur l'API .NET.
# Construite depuis la racine du dépôt : docker build -f docker/web.Dockerfile .
#
# `next start` plutôt que la sortie autonome : `next.config.ts` trace depuis `../..`
# (arborescence de Speedy) et on ne modifie pas le code du front. Seuls le web et
# `@delivery/shared` sont installés : jamais le mobile.
FROM node:22-bookworm-slim
ENV PNPM_HOME=/pnpm PATH=/pnpm:$PATH COREPACK_ENABLE_DOWNLOAD_PROMPT=0 NEXT_TELEMETRY_DISABLED=1
RUN corepack enable
WORKDIR /repo/wimo

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY frontend/package.json frontend/
COPY mobile/package.json mobile/
COPY packages/shared/package.json packages/shared/
COPY packages/config/package.json packages/config/
RUN pnpm install --frozen-lockfile --filter "@delivery/web..."

COPY packages/ packages/
COPY frontend/ frontend/

# Figé dans le bundle au build ; le serveur Next lit `API_URL` à l'exécution.
ARG NEXT_PUBLIC_API_URL=http://localhost:5080/api
ARG NEXT_PUBLIC_PLATFORM_DOMAIN=localhost
ENV NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL} NEXT_PUBLIC_PLATFORM_DOMAIN=${NEXT_PUBLIC_PLATFORM_DOMAIN}
RUN pnpm --filter @delivery/shared build && pnpm --filter @delivery/web build

ENV NODE_ENV=production PORT=3000 HOSTNAME=0.0.0.0
EXPOSE 3000
CMD ["pnpm", "--filter", "@delivery/web", "exec", "next", "start", "-p", "3000", "-H", "0.0.0.0"]
