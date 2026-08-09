FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci --prefer-offline

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS runtime

RUN addgroup -S appgroup && adduser -S appuser -G appgroup

COPY --from=build /app/dist/product-management-client/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

RUN chown -R appuser:appgroup /usr/share/nginx/html \
    && chown -R appuser:appgroup /var/cache/nginx \
    && chown -R appuser:appgroup /var/log/nginx \
    && touch /var/run/nginx.pid \
    && chown appuser:appgroup /var/run/nginx.pid

USER appuser

EXPOSE 80
