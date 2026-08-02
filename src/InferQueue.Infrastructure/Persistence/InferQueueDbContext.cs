using InferQueue.Core.Jobs;
using Microsoft.EntityFrameworkCore;

namespace InferQueue.Infrastructure.Persistence;

public sealed class InferQueueDbContext(DbContextOptions<InferQueueDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(InferQueueDbContext).Assembly);
}
