# ─── Build stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SERVICE_PATH
ARG PROJECT_FILE

WORKDIR /src
COPY . .
RUN dotnet restore "${SERVICE_PATH}/${PROJECT_FILE}" --disable-parallel
RUN dotnet publish "${SERVICE_PATH}/${PROJECT_FILE}" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ─── Runtime stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ARG ASSEMBLY_NAME
ENV DOTNET_ASSEMBLY_NAME=${ASSEMBLY_NAME}

EXPOSE 80
ENTRYPOINT ["sh", "-c", "exec dotnet ${DOTNET_ASSEMBLY_NAME}.dll"]
