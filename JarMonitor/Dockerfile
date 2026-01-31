FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
RUN apt-get update \
    && apt-get install -y --no-install-recommends fontconfig fonts-liberation libuuid1 patchelf \
    && rm -rf /var/lib/apt/lists/*
RUN rm -f /usr/share/fontconfig/conf.avail/05-reset-dirs-sample.conf
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
WORKDIR /src/JarMonitor
RUN dotnet publish -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=build /app .
RUN patchelf --add-needed libuuid.so.1 /app/runtimes/linux-arm64/native/libSkiaSharp.so
ENTRYPOINT ["dotnet", "JarMonitor.dll"]