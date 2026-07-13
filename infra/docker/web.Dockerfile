FROM node:22-alpine AS dependencies
WORKDIR /app
COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci

FROM node:22-alpine AS build
ARG NEXT_PUBLIC_WMS_API_URL
ENV NEXT_PUBLIC_WMS_API_URL=${NEXT_PUBLIC_WMS_API_URL}
WORKDIR /app
COPY --from=dependencies /app/node_modules ./node_modules
COPY apps/web/package.json apps/web/package-lock.json ./
COPY apps/web/next.config.ts apps/web/next-env.d.ts apps/web/tsconfig.json ./
COPY apps/web/app ./app
COPY apps/web/components ./components
COPY apps/web/lib ./lib
RUN npm run build

FROM node:22-alpine AS runtime
ENV NODE_ENV=production
WORKDIR /app
RUN addgroup --system --gid 1001 nodejs && adduser --system --uid 1001 nextjs
COPY --from=build --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/.next/static ./.next/static
USER nextjs
EXPOSE 3000
CMD ["node", "server.js"]
