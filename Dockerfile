# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Make sure CA roots exist (and allow injecting corporate root CA via build secret)
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Optional: inject corporate root CA (for NuGet/HTTPS behind SSL inspection)
# Usage:
#   docker buildx build --secret id=corp_ca,src=corp-ca.crt ...
RUN --mount=type=secret,id=corp_ca,required=false sh -c '\
    if [ -f /run/secrets/corp_ca ]; then \
      cp /run/secrets/corp_ca /usr/local/share/ca-certificates/corp_ca.crt && \
      update-ca-certificates; \
    fi'

# Project is in repository root (this folder)
COPY ["BusinessCalendarAPI.csproj", "./"]
RUN dotnet restore "./BusinessCalendarAPI.csproj"
COPY . .
RUN dotnet build "./BusinessCalendarAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BusinessCalendarAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BusinessCalendarAPI.dll"]