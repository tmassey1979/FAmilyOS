FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props .
COPY FamilyOS.sln .
COPY src/FamilyOS.Domain/FamilyOS.Domain.csproj src/FamilyOS.Domain/
COPY src/FamilyOS.Contracts/FamilyOS.Contracts.csproj src/FamilyOS.Contracts/
COPY src/FamilyOS.Application/FamilyOS.Application.csproj src/FamilyOS.Application/
COPY src/FamilyOS.Infrastructure/FamilyOS.Infrastructure.csproj src/FamilyOS.Infrastructure/
COPY src/FamilyOS.Api/FamilyOS.Api.csproj src/FamilyOS.Api/
RUN dotnet restore src/FamilyOS.Api/FamilyOS.Api.csproj
COPY src/ src/
COPY scripts/ scripts/
RUN python3 scripts/expand-migrations.py || true
RUN dotnet publish src/FamilyOS.Api/FamilyOS.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FamilyOS.Api.dll"]
