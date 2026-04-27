FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Shared/Shared.csproj", "Shared/"]
COPY ["AvailabilityService/AvailabilityService.csproj", "AvailabilityService/"]
RUN dotnet restore "AvailabilityService/AvailabilityService.csproj"
COPY . .
RUN dotnet publish "AvailabilityService/AvailabilityService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AvailabilityService.dll"]
