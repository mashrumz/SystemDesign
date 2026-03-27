FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["WriteService/WriteService.csproj", "WriteService/"]
COPY ["Shared/Shared.csproj", "Shared/"]
RUN dotnet restore "WriteService/WriteService.csproj"
COPY . .
WORKDIR "/src/WriteService"
RUN dotnet build "WriteService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WriteService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WriteService.dll"]