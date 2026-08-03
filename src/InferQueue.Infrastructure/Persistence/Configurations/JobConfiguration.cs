using InferQueue.Core.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InferQueue.Infrastructure.Persistence.Configurations;

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnName("id");

        builder.Property(j => j.InputHash)
            .HasColumnName("input_hash")
            .HasColumnType("char(64)")
            .IsRequired();

        builder.Property(j => j.InputText)
            .HasColumnName("input_text")
            .IsRequired();

        builder.Property(j => j.Model)
            .HasColumnName("model")
            .HasMaxLength(100)
            .IsRequired();

        // Guardado como texto e nao como int: quem abrir o banco le 'Pending', nao '0'.
        builder.Property(j => j.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(j => j.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(j => j.NextAttemptAt).HasColumnName("next_attempt_at").IsRequired();
        builder.Property(j => j.LockedUntil).HasColumnName("locked_until");

        builder.Property(j => j.Result).HasColumnName("result").HasColumnType("jsonb");
        builder.Property(j => j.Error).HasColumnName("error");

        builder.Property(j => j.PromptTokens).HasColumnName("prompt_tokens");
        builder.Property(j => j.CompletionTokens).HasColumnName("completion_tokens");
        builder.Property(j => j.CostUsd).HasColumnName("cost_usd").HasColumnType("numeric(10,6)");

        builder.Property(j => j.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(j => j.CompletedAt).HasColumnName("completed_at");

        // Indice que serve a consulta do worker (status + prazo do backoff).
        // Parcial: so as linhas Pending entram, entao ele nao cresce junto com o historico de jobs concluidos.
        builder.HasIndex(j => new { j.Status, j.NextAttemptAt })
            .HasDatabaseName("ix_jobs_pending")
            .HasFilter("status = 'Pending'");

        // Indice de busca por hash, sem unicidade. A deduplicacao acontece no enqueue,
        // nao aqui: exigir hash unico entre os concluidos proibiria reprocessar o mesmo
        // texto meses depois, que e um caso legitimo.
        // Os dois indices abaixo cobrem a mesma coluna e por isso precisam da sobrecarga
        // com nome: HasIndex(coluna) chamado duas vezes reconfigura o mesmo indice em vez
        // de criar outro, e o primeiro desaparece sem aviso.

        // Busca por hash no historico — e o que o enqueue usa para reaproveitar um Done.
        builder.HasIndex(j => j.InputHash, "ix_jobs_input_hash");

        // No maximo um job em andamento por conteudo. Parcial de proposito: o historico de
        // concluidos pode repetir o hash a vontade; o que nao pode e haver duas execucoes
        // simultaneas do mesmo trabalho. E este indice que resolve a corrida entre dois POSTs
        // concorrentes — a consulta previa, sozinha, nao resolveria.
        builder.HasIndex(j => j.InputHash, "ux_jobs_input_hash_inflight")
            .IsUnique()
            .HasFilter("status IN ('Pending', 'Processing')");
    }
}
