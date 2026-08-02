using InferQueue.Core.Jobs;
using InferQueue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InferQueue.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' nao configurada. Veja o appsettings.json.");

        services.AddDbContext<InferQueueDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IJobStore, EfJobStore>();

        return services;
    }
}
