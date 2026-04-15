FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["DiffCheck.Cli/DiffCheck.Cli.csproj", "DiffCheck.Cli/"]
COPY ["DiffCheck.Core/DiffCheck.Core.csproj", "DiffCheck.Core/"]

RUN dotnet restore "DiffCheck.Cli/DiffCheck.Cli.csproj"

COPY . .
WORKDIR /src
RUN rm -f global.json && dotnet publish "DiffCheck.Cli/DiffCheck.Cli.csproj" -c Release -o /app/publish \
    --self-contained

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

LABEL org.opencontainers.image.source="https://github.com/milvasic/diffcheck"
LABEL org.opencontainers.image.description="CLI tool for comparing CSV and XLSX files"

ENTRYPOINT ["./diffcheck"]
