# syntax=docker/dockerfile:1.7
#
# Image du front — Next.js en sortie autonome.
#
# Construite depuis la RACINE du monorepo, comme celle de l'API :
#
#   docker build -f apps/web/Dockerfile -t delivery-web .

ARG NODE_IMAGE=node:22-bookworm-slim

# ---------------------------------------------------------------- base --------
FROM ${NODE_IMAGE} AS base
ENV PNPM_HOME=/pnpm \
    PATH=/pnpm:$PATH \
    COREPACK_ENABLE_DOWNLOAD_PROMPT=0
RUN corepack enable
WORKDIR /app

# --------------------------------------------------------------- deps ---------
FROM base AS deps
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/api/package.json ./apps/api/
COPY apps/web/package.json ./apps/web/
COPY packages/shared/package.json ./packages/shared/
COPY packages/config/package.json ./packages/config/
RUN --mount=type=cache,id=pnpm-store,target=/pnpm/store \
    pnpm install --frozen-lockfile

# -------------------------------------------------------------- build ---------
FROM base AS build
COPY --from=deps /app/node_modules ./node_modules
COPY --from=deps /app/apps/web/node_modules ./apps/web/node_modules
COPY --from=deps /app/packages/shared/node_modules ./packages/shared/node_modules
COPY . .

ENV NEXT_TELEMETRY_DISABLED=1

# `NEXT_PUBLIC_*` est figé DANS le bundle au moment du build : il doit donc être
# fourni ici, pas au démarrage. Les appels réels partent du serveur et lisent
# `API_URL` à l'exécution (DECISIONS D-018) ; cette valeur n'est qu'un repli.
ARG NEXT_PUBLIC_API_URL=http://localhost:3001/api
ENV NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL}

# Le pré-rendu appelle l'API pour la marque et la grille tarifaire. Si elle
# n'est pas joignable au moment du build — ce qui est le cas normal, la pile
# n'étant pas encore levée — les deux lectures dégradent au lieu d'échouer
# (D-016), et l'ISR repeuple au premier rendu.
RUN pnpm --filter @delivery/shared build \
 && pnpm --filter @delivery/web build

# ------------------------------------------------------------- runner ---------
FROM base AS runner
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

# La sortie autonome embarque son propre sous-ensemble de `node_modules` et un
# serveur minimal : l'image finale n'a ni gestionnaire de paquets, ni
# dépendances de développement, ni sources.
COPY --from=build --chown=node:node /app/apps/web/.next/standalone ./
# `static` et `public` ne sont pas tracés par la sortie autonome : ils sont
# servis en tant que fichiers, pas importés par du code.
COPY --from=build --chown=node:node /app/apps/web/.next/static ./apps/web/.next/static
COPY --from=build --chown=node:node /app/apps/web/public ./apps/web/public

USER node
EXPOSE 3000

# La sonde vise un fichier STATIQUE, pas une page rendue — et c'est délibéré
# depuis C8. Une page appelle l'API, qui résout la société par l'hôte ; la sonde
# frappe `127.0.0.1`, qui n'est ni le domaine de la plateforme ni celui d'un
# client. `/fr` y répondait donc 500 « contexte de tenant absent », et le
# conteneur restait `unhealthy` à vie alors que tous les vrais hôtes rendaient
# 200 — une supervision qui ment en permanence ne sert plus à rien.
#
# `healthz.txt` porte un point : le matcher du proxy (`apps/web/src/proxy.ts`)
# l'exclut de l'i18n, la requête ne traverse donc aucune résolution d'hôte.
# Ce que la sonde affirme est ce qu'un conteneur peut honnêtement affirmer : le
# serveur Node est levé et sert. L'état de l'API a sa propre sonde.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD node -e "fetch('http://127.0.0.1:'+(process.env.PORT||3000)+'/healthz.txt').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"

CMD ["node", "apps/web/server.js"]
