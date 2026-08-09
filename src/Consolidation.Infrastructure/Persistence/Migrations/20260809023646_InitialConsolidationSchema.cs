using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Consolidation.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialConsolidationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_balances",
                columns: table => new
                {
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_credits = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_debits = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_balances", x => x.date);
                });

            migrationBuilder.CreateTable(
                name: "processed_events",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_events", x => x.event_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_balances");

            migrationBuilder.DropTable(
                name: "processed_events");
        }
    }
}
