using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pubquiz_Platform_V2.Migrations
{
    /// <inheritdoc />
    public partial class AddLobbies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LobbyId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Lobbies",
                columns: table => new
                {
                    LobbyId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LobbyCode = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    QuizId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lobbies", x => x.LobbyId);
                    table.ForeignKey(
                        name: "FK_Lobbies_Quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "Quizzes",
                        principalColumn: "QuizId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_LobbyId",
                table: "Users",
                column: "LobbyId");

            migrationBuilder.CreateIndex(
                name: "IX_Lobbies_QuizId",
                table: "Lobbies",
                column: "QuizId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Lobbies_LobbyId",
                table: "Users",
                column: "LobbyId",
                principalTable: "Lobbies",
                principalColumn: "LobbyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Lobbies_LobbyId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Lobbies");

            migrationBuilder.DropIndex(
                name: "IX_Users_LobbyId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LobbyId",
                table: "Users");
        }
    }
}
