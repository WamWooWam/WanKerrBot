using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WamBot.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Canvases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Canvases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Layer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CanvasId = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    BlendingMode = table.Column<int>(type: "INTEGER", nullable: false),
                    Opacity = table.Column<double>(type: "REAL", nullable: false),
                    LayerData = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Layer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Layer_Canvases_CanvasId",
                        column: x => x.CanvasId,
                        principalTable: "Canvases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Color_R = table.Column<byte>(type: "INTEGER", nullable: true),
                    Color_G = table.Column<byte>(type: "INTEGER", nullable: true),
                    Color_B = table.Column<byte>(type: "INTEGER", nullable: true),
                    CanvasId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => new { x.GuildId, x.UserId });
                    table.ForeignKey(
                        name: "FK_Members_Canvases_CanvasId",
                        column: x => x.CanvasId,
                        principalTable: "Canvases",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Layer_CanvasId",
                table: "Layer",
                column: "CanvasId");

            migrationBuilder.CreateIndex(
                name: "IX_Layer_Position",
                table: "Layer",
                column: "Position");

            migrationBuilder.CreateIndex(
                name: "IX_Members_CanvasId",
                table: "Members",
                column: "CanvasId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Layer");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "Canvases");
        }
    }
}
