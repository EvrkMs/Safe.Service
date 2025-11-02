using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Safe.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "SafeChanges",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ModifiedAt",
                table: "SafeChanges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "SafeChanges",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SafeChanges",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_SafeChanges_CreatedBy",
                table: "SafeChanges",
                column: "CreatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SafeChanges_CreatedBy",
                table: "SafeChanges");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SafeChanges");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "SafeChanges");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "SafeChanges");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SafeChanges");
        }
    }
}
