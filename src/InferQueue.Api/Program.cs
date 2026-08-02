using InferQueue.Api.Endpoints;
using InferQueue.Api.ErrorHandling;
using InferQueue.Infrastructure;
using InferQueue.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<InferQueueDbContext>();

var app = builder.Build();

// Qualquer excecao nao tratada vira um ProblemDetails (RFC 9457) em vez de vazar stack trace.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health");
app.MapJobEndpoints();

app.Run();
