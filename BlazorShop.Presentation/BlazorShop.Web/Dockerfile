# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

# Copy solution and project files
COPY *.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .

# Copy all project files
COPY BlazorShop.Domain/*.csproj BlazorShop.Domain/
COPY BlazorShop.Application/*.csproj BlazorShop.Application/
COPY BlazorShop.Infrastructure/*.csproj BlazorShop.Infrastructure/
COPY BlazorShop.ServiceDefaults/*.csproj BlazorShop.ServiceDefaults/
COPY BlazorShop.Presentation/BlazorShop.Web/*.csproj BlazorShop.Presentation/BlazorShop.Web/
COPY BlazorShop.Presentation/BlazorShop.Web.Shared/*.csproj BlazorShop.Presentation/BlazorShop.Web.Shared/
COPY BlazorShop.Tests/*.csproj BlazorShop.Tests/

# Restore dependencies
RUN dotnet restore BlazorShop.Presentation/BlazorShop.Web/BlazorShop.Web.csproj

# Copy all source code
COPY . .

# Build and publish the application
WORKDIR /app/BlazorShop.Presentation/BlazorShop.Web
RUN dotnet publish -c Release -o out --no-restore

# Runtime stage - use nginx to serve the static files
FROM nginx:alpine AS runtime

# Copy custom nginx config
COPY <<EOF /etc/nginx/nginx.conf
events { worker_connections 1024; }

http {
    include       /etc/nginx/mime.types;
    default_type  application/octet-stream;

    # Logging
    log_format main '\$remote_addr - \$remote_user [\$time_local] "\$request" '
                    '\$status \$body_bytes_sent "\$http_referer" '
                    '"\$http_user_agent" "\$http_x_forwarded_for"';
    
    access_log /var/log/nginx/access.log main;
    error_log /var/log/nginx/error.log warn;

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_proxied any;
    gzip_comp_level 6;
    gzip_types
        text/plain
        text/css
        text/xml
        text/javascript
        application/json
        application/javascript
        application/xml+rss
        application/atom+xml
        image/svg+xml;

    server {
        listen 80;
        server_name localhost;
        root /usr/share/nginx/html;
        index index.html;

        # Security headers
        add_header X-Frame-Options DENY;
        add_header X-Content-Type-Options nosniff;
        add_header X-XSS-Protection "1; mode=block";
        add_header Referrer-Policy strict-origin-when-cross-origin;

        # Handle client-side routing
        location / {
            try_files \$uri \$uri/ /index.html;
        }

        # Cache static assets
        location ~* \.(?:css|js|jpg|jpeg|gif|png|ico|cur|gz|svg|svgz|mp4|ogg|ogv|webm|htc|woff|woff2)$ {
            expires 1y;
            add_header Cache-Control "public, immutable";
        }

        # Cache service worker
        location = /service-worker.js {
            expires off;
            add_header Cache-Control "public, max-age=0";
        }

        # Health check endpoint
        location /health {
            access_log off;
            return 200 "healthy\n";
            add_header Content-Type text/plain;
        }
    }
}
EOF

# Copy the built app to nginx html directory
COPY --from=build-env /app/BlazorShop.Presentation/BlazorShop.Web/out/wwwroot /usr/share/nginx/html

# Create non-root user for security
RUN addgroup -g 1001 -S appuser && \
    adduser -S -D -H -u 1001 -h /var/cache/nginx -s /sbin/nologin -G appuser -g appuser appuser && \
    chown -R appuser:appuser /var/cache/nginx /var/log/nginx && \
    chown -R appuser:appuser /usr/share/nginx/html

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost/health || exit 1

# Start nginx
CMD ["nginx", "-g", "daemon off;"]