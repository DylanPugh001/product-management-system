FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ProductManagementSystem.Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

COPY --from=build /app/publish .

RUN mkdir -p /data && chown appuser:appgroup /data

USER appuser

ENV ASPNETCORE_URLS=http://+:5001
ENV ConnectionStrings__DefaultConnection=Data Source=/data/productmanagement.db

EXPOSE 5001

ENTRYPOINT ["dotnet", "ProductManagementSystem.Api.dll"]
