FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Shared/Shared.csproj", "Shared/"]
COPY ["NearbyService/NearbyService.csproj", "NearbyService/"]
RUN dotnet restore "NearbyService/NearbyService.csproj"
COPY . .
RUN dotnet publish "NearbyService/NearbyService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NearbyService.dll"]
