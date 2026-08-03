using InferQueue.Infrastructure;
using InferQueue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace InferQueue.IntegrationTests;

/// <summary>
/// Sobe um Postgres real em container e aplica as migrations. Um banco de verdade e
/// obrigatorio aqui: metade do que estes testes verificam — <c>FOR UPDATE SKIP LOCKED</c>,
/// indice unico parcial, agregacao — simplesmente nao existe num provider em memoria.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Mesma imagem do docker-compose: testar contra uma versao diferente da que roda
    // em desenvolvimento tiraria metade do valor de usar um banco real.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    private ServiceProvider? _provider;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _container.GetConnectionString(),
                ["Llm:DefaultModel"] = "gpt-4o-mini"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Passa pelo AddInfrastructure de verdade: assim o proprio registro no container
        // de DI fica coberto, e nao so as classes isoladas.
        services.AddInfrastructure(configuration);

        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InferQueueDbContext>();
        await db.Database.MigrateAsync();
    }

    public AsyncServiceScope CreateScope() => _provider!.CreateAsyncScope();

    /// <summary>Limpa a tabela entre testes — o container e compartilhado pela colecao.</summary>
    public async Task ResetAsync()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InferQueueDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE jobs;");
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
