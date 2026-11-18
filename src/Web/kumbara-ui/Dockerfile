# =============================================================================
# Stage 1: Build the Angular Standalone Application
# =============================================================================
FROM node:20-alpine AS build
WORKDIR /app

# Install dependencies
COPY package*.json ./
RUN npm ci

# Copy sources and build production bundle
COPY . .
RUN npm run build -- --configuration production

# =============================================================================
# Stage 2: Serve Built Static Assets with Nginx
# =============================================================================
FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/kumbara-ui/browser /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
