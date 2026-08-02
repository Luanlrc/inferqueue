using InferQueue.Core.Jobs;
using Microsoft.EntityFrameworkCore;

namespace InferQueue.Infrastructure.Persistence;

internal sealed class EfJobStore(InferQueueDbContext db) : IJobStore
{
    public async Task AddAsync(Job job, CancellationToken ct = default)
    {
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
    }

    public Task<Job?> GetAsync(Guid id, CancellationToken ct = default)
        => db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);
}
