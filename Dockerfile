FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 4080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["HubLink.Server/HubLink.Server.csproj", "HubLink.Server/"]
COPY ["HubLink.Shared/HubLink.Shared.csproj", "HubLink.Shared/"]
RUN dotnet restore "HubLink.Server/HubLink.Server.csproj"
COPY . .
WORKDIR "/src/HubLink.Server"
RUN dotnet build "HubLink.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HubLink.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HubLink.Server.dll"]
