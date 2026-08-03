using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InferQueue.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDoneHashUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_jobs_input_hash_done",
                table: "jobs");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_input_hash",
                table: "jobs",
                column: "input_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_jobs_input_hash",
                table: "jobs");

            migrationBuilder.CreateIndex(
                name: "ux_jobs_input_hash_done",
                table: "jobs",
                column: "input_hash",
                unique: true,
                filter: "status = 'Done'");
        }
    }
}
