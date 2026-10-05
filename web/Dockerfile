# syntax=docker/dockerfile:1
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run check && npm test && npm run build && npm prune --omit=dev

# Runtime: the SvelteKit BFF (Node). Serves the PWA, renders the office pages, talks to the internal API.
FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production PORT=3000 BODY_SIZE_LIMIT=26214400
COPY --from=build /app/build build
COPY --from=build /app/node_modules node_modules
COPY package.json .
USER node
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --start-interval=1s CMD wget -qO- http://127.0.0.1:3000/healthz >/dev/null || exit 1
CMD ["node", "build"]
