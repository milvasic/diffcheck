FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["DiffCheck.Web/DiffCheck.Web.csproj", "DiffCheck.Web/"]
COPY ["DiffCheck.Core/DiffCheck.Core.csproj", "DiffCheck.Core/"]

RUN dotnet restore "DiffCheck.Web/DiffCheck.Web.csproj"

COPY . .
WORKDIR /src
RUN rm -f global.json && dotnet publish "DiffCheck.Web/DiffCheck.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

LABEL org.opencontainers.image.source="https://github.com/milvasic/diffcheck"
LABEL org.opencontainers.image.description="Web UI for comparing CSV and XLSX files"

ENTRYPOINT ["dotnet", "DiffCheck.Web.dll"]
