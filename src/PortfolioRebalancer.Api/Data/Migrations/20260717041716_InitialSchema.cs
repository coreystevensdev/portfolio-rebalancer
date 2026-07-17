using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioRebalancer.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Portfolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DriftTolerancePct = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Portfolios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Holdings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ticker = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holdings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holdings_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RebalancingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RebalancingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RebalancingEvents_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TargetAllocation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ticker = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetWeight = table.Column<decimal>(type: "numeric(8,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetAllocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TargetAllocation_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Lot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Shares = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    CostBasisPerShare = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PurchasedAt = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lot_Holdings_HoldingId",
                        column: x => x.HoldingId,
                        principalTable: "Holdings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RebalancingOrder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ticker = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Shares = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    EstimatedValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RebalancingOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RebalancingOrder_RebalancingEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "RebalancingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Holdings_PortfolioId",
                table: "Holdings",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_Lot_HoldingId",
                table: "Lot",
                column: "HoldingId");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_UserId",
                table: "Portfolios",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RebalancingEvents_PortfolioId",
                table: "RebalancingEvents",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_RebalancingOrder_EventId",
                table: "RebalancingOrder",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_TargetAllocation_PortfolioId",
                table: "TargetAllocation",
                column: "PortfolioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Lot");

            migrationBuilder.DropTable(
                name: "RebalancingOrder");

            migrationBuilder.DropTable(
                name: "TargetAllocation");

            migrationBuilder.DropTable(
                name: "Holdings");

            migrationBuilder.DropTable(
                name: "RebalancingEvents");

            migrationBuilder.DropTable(
                name: "Portfolios");
        }
    }
}
