using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InferQueue.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInFlightDedupIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_jobs_input_hash_inflight",
                table: "jobs",
                column: "input_hash",
                unique: true,
                filter: "status IN ('Pending', 'Processing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_jobs_input_hash_inflight",
                table: "jobs");
        }
    }
}
