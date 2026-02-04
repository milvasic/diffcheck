FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["DiffCheck.Web/DiffCheck.Web.csproj", "DiffCheck.Web/"]
COPY ["DiffCheck.Core/DiffCheck.Core.csproj", "DiffCheck.Core/"]

RUN dotnet restore "DiffCheck.Web/DiffCheck.Web.csproj"

COPY . .
WORKDIR /src
RUN dotnet publish "DiffCheck.Web/DiffCheck.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DiffCheck.Web.dll"]
