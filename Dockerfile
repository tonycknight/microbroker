ARG BuildVersion

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Creates a non-root user with an explicit UID and adds permission to access the /app folder
# For more info, please refer to https://aka.ms/vscode-docker-dotnet-configure-containers
RUN adduser -u 5678 --disabled-password --gecos "" microbrokeruser && chown -R microbrokeruser /app
USER microbrokeruser

FROM mcr.microsoft.com/dotnet/sdk:8.0.100 AS build
ARG BuildVersion
WORKDIR /src

COPY ["src/microbroker/microbroker.fsproj", "src/microbroker/"]
RUN dotnet restore "src/microbroker/microbroker.fsproj"
COPY . .
WORKDIR "/src/src/microbroker"
RUN dotnet tool restore
RUN dotnet paket restore
RUN dotnet build "microbroker.fsproj" -c Release -o /app/build /p:AssemblyInformationalVersion=${BuildVersion} /p:AssemblyFileVersion=${BuildVersion}

FROM build AS publish
ARG BuildVersion
RUN dotnet publish "microbroker.fsproj" -c Release -o /app/publish /p:UseAppHost=true /p:AssemblyInformationalVersion=${BuildVersion} /p:AssemblyFileVersion=${BuildVersion} /p:Version=${BuildVersion}  --os linux --arch x64 --self-contained

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "microbroker.dll", ""]
