# Contexto de build e a raiz do repositorio, nao esta pasta.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/InferQueue.Core/InferQueue.Core.csproj src/InferQueue.Core/
COPY src/InferQueue.Infrastructure/InferQueue.Infrastructure.csproj src/InferQueue.Infrastructure/
COPY src/InferQueue.Worker/InferQueue.Worker.csproj src/InferQueue.Worker/
RUN dotnet restore src/InferQueue.Worker/InferQueue.Worker.csproj

COPY src/ src/
RUN dotnet publish src/InferQueue.Worker/InferQueue.Worker.csproj -c Release -o /app --no-restore

# runtime e nao aspnet: o worker nao serve HTTP, entao nao precisa do ASP.NET Core embarcado.
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .

USER $APP_UID

ENTRYPOINT ["dotnet", "InferQueue.Worker.dll"]
