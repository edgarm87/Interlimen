FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Meridian.Web/Meridian.Web.csproj", "src/Meridian.Web/"]
COPY ["src/Meridian.Application/Meridian.Application.csproj", "src/Meridian.Application/"]
COPY ["src/Meridian.Domain/Meridian.Domain.csproj", "src/Meridian.Domain/"]
COPY ["src/Meridian.Infrastructure/Meridian.Infrastructure.csproj", "src/Meridian.Infrastructure/"]
RUN dotnet restore "src/Meridian.Web/Meridian.Web.csproj"
COPY . .
WORKDIR "/src/src/Meridian.Web"
RUN dotnet build "Meridian.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Meridian.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Meridian.Web.dll"]
