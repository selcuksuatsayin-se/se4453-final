# Build aşaması
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Sadece proje dosyasını kopyalayıp restore et (Hatayı bu satır çözer)
COPY midterm_group4.csproj ./
RUN dotnet restore "midterm_group4.csproj"

# Geri kalan her şeyi kopyala
COPY . .
# Yayınlama sırasında proje adını belirt
RUN dotnet publish "midterm_group4.csproj" -c Release -o out

# Çalışma aşaması
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Başlatma betiği [cite: 156]
COPY init.sh /app/init.sh
RUN chmod +x /app/init.sh

EXPOSE 8080 2222
ENTRYPOINT ["/app/init.sh"]

ENV ASPNETCORE_URLS=http://+:80