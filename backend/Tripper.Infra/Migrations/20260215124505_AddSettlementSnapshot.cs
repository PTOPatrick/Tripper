using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripper.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SettlementSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RatesAsOfUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ItemsIncludedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementSnapshots_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SettlementTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementTransfers_SettlementSnapshots_SettlementSnapshotId",
                        column: x => x.SettlementSnapshotId,
                        principalTable: "SettlementSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementSnapshots_GroupId_CreatedAt",
                table: "SettlementSnapshots",
                columns: new[] { "GroupId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementTransfers_SettlementSnapshotId",
                table: "SettlementTransfers",
                column: "SettlementSnapshotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettlementTransfers");

            migrationBuilder.DropTable(
                name: "SettlementSnapshots");
        }
    }
}
