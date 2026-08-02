# Contexto de build e a raiz do repositorio, nao esta pasta.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Os csproj vem primeiro e sozinhos: assim a camada de restore so e invalidada quando
# uma dependencia muda, e nao a cada linha de codigo editada.
COPY src/InferQueue.Core/InferQueue.Core.csproj src/InferQueue.Core/
COPY src/InferQueue.Infrastructure/InferQueue.Infrastructure.csproj src/InferQueue.Infrastructure/
COPY src/InferQueue.Api/InferQueue.Api.csproj src/InferQueue.Api/
RUN dotnet restore src/InferQueue.Api/InferQueue.Api.csproj

COPY src/ src/
RUN dotnet publish src/InferQueue.Api/InferQueue.Api.csproj -c Release -o /app --no-restore

# A imagem final leva so o publicado: o SDK inteiro passaria de 800MB.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Usuario nao-root ja definido pela imagem base.
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "InferQueue.Api.dll"]
