using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pubquiz_Platform_V2.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizSlugForGoodUrlString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quizzes_QuizMasterId",
                table: "Quizzes");

            migrationBuilder.AddColumn<string>(
                name: "QuizSlug",
                table: "Quizzes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_QuizMasterId_QuizSlug",
                table: "Quizzes",
                columns: new[] { "QuizMasterId", "QuizSlug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quizzes_QuizMasterId_QuizSlug",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "QuizSlug",
                table: "Quizzes");

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_QuizMasterId",
                table: "Quizzes",
                column: "QuizMasterId");
        }
    }
}
