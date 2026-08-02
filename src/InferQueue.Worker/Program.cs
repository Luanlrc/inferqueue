using InferQueue.Core.Jobs;
using InferQueue.Infrastructure;
using InferQueue.Worker;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IOptions<WorkerOptions>>().Value.ToRetryPolicy());

builder.Services.AddHostedService<JobDispatcher>();
builder.Services.AddHostedService<LeaseReaper>();

var host = builder.Build();
host.Run();
