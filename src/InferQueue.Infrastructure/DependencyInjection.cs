using System.Net.Http.Headers;
using InferQueue.Core.Jobs;
using InferQueue.Core.Llm;
using InferQueue.Infrastructure.Llm;
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

        services.AddOptions<LlmOptions>()
            .Bind(configuration.GetSection(LlmOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        AddLlmClient(services, configuration);

        return services;
    }

    private static void AddLlmClient(IServiceCollection services, IConfiguration configuration)
    {
        var llm = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();

        // Sem chave, o sistema nao quebra: cai no fake. Isso mantem `docker compose up`
        // + `dotnet run` funcionando para quem clonar o repo sem conta na OpenAI.
        if (string.IsNullOrWhiteSpace(llm.ApiKey))
        {
            services.AddSingleton<ILlmClient, FakeLlmClient>();
            return;
        }

        services.AddHttpClient<ILlmClient, OpenAiLlmClient>(client =>
        {
            client.BaseAddress = new Uri(llm.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(llm.TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", llm.ApiKey);
        });
    }
}
