FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && \
    npm install -g wrangler && \
    apt-get remove -y curl && apt-get autoremove -y
WORKDIR /app
COPY --from=build /src/out .
ENTRYPOINT ["dotnet", "CloudflareWorkerBot.dll"]