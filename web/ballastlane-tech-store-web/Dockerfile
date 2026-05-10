# Build the React bundle. Build args bake the API URLs into the bundle since Vite
# resolves import.meta.env at build time, not runtime.
FROM node:20-alpine AS build
ARG VITE_AUTH_API
ARG VITE_STORE_API
ENV VITE_AUTH_API=${VITE_AUTH_API}
ENV VITE_STORE_API=${VITE_STORE_API}
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

# Serve the static bundle through nginx. SPA fallback so client-side routes work.
FROM nginx:1.27-alpine AS runtime
COPY --from=build /src/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
