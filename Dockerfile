# Build aşaması
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Çalışma (Runtime) aşaması
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# SSH Kurulumu ve Yapılandırması (Azure App Service gereksinimi) [cite: 154]
RUN apt-get update && apt-get install -y --no-install-recommends openssh-server \
    && echo "root:Docker!" | chpasswd
COPY sshd_config /etc/ssh/

# Başlatma betiğini kopyala ve izin ver 
COPY init.sh /app/init.sh
RUN chmod +x /app/init.sh

# Web portu ve SSH portunu aç 
EXPOSE 80 2222

ENTRYPOINT ["/app/init.sh"]