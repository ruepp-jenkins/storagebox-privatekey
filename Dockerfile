FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY StorageBoxKeyTool.sln ./
COPY StorageBoxKeyTool.Api/StorageBoxKeyTool.Api.csproj StorageBoxKeyTool.Api/
COPY StorageBoxKeyTool.Client/StorageBoxKeyTool.Client.csproj StorageBoxKeyTool.Client/
COPY StorageBoxKeyTool.Tests/StorageBoxKeyTool.Tests.csproj StorageBoxKeyTool.Tests/

RUN dotnet restore StorageBoxKeyTool.sln -a $TARGETARCH

COPY . .

RUN dotnet publish StorageBoxKeyTool.Api/StorageBoxKeyTool.Api.csproj \
    -c Release \
    -o /app/publish \
    -a $TARGETARCH \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

RUN apt-get update \
    && apt-get install -y --no-install-recommends openssh-client \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "StorageBoxKeyTool.Api.dll"]
