using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Safe.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SafeChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReversalOfChangeId = table.Column<long>(type: "bigint", nullable: true),
                    ReversalComment = table.Column<string>(type: "text", nullable: true),
                    ReversedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafeChanges", x => x.Id);
                    table.CheckConstraint("ck_safechange_amount_positive", "amount > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SafeChanges_OccurredAt",
                table: "SafeChanges",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_SafeChanges_Reason",
                table: "SafeChanges",
                column: "Reason");

            migrationBuilder.CreateIndex(
                name: "IX_SafeChanges_Status",
                table: "SafeChanges",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SafeChanges");
        }
    }
}
