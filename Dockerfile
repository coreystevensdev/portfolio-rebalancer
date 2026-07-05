FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /app

COPY PortfolioRebalancer.sln ./
COPY src/PortfolioRebalancer.Api/PortfolioRebalancer.Api.csproj src/PortfolioRebalancer.Api/
RUN dotnet restore src/PortfolioRebalancer.Api/PortfolioRebalancer.Api.csproj

COPY src/ src/
RUN dotnet publish src/PortfolioRebalancer.Api/PortfolioRebalancer.Api.csproj \
    -c Release -o /publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=builder /publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "PortfolioRebalancer.Api.dll"]
