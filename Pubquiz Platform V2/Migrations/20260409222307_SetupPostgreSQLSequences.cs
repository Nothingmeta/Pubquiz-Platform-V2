using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pubquiz_Platform_V2.Migrations
{
    /// <inheritdoc />
    public partial class SetupPostgreSQLSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only PostgreSQL needs sequences - SQLite ignores this
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    DO $$
                    BEGIN
                        -- Users.UserId sequence
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.sequences 
                            WHERE sequence_name = 'Users_UserId_seq'
                        ) THEN
                            CREATE SEQUENCE ""Users_UserId_seq"" START WITH 1 INCREMENT BY 1;
                            ALTER TABLE ""Users""
                            ALTER COLUMN ""UserId"" SET DEFAULT nextval('""Users_UserId_seq""');
                        END IF;

                        -- Quizzes.QuizId sequence
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.sequences 
                            WHERE sequence_name = 'Quizzes_QuizId_seq'
                        ) THEN
                            CREATE SEQUENCE ""Quizzes_QuizId_seq"" START WITH 1 INCREMENT BY 1;
                            ALTER TABLE ""Quizzes""
                            ALTER COLUMN ""QuizId"" SET DEFAULT nextval('""Quizzes_QuizId_seq""');
                        END IF;

                        -- Questions.QuestionId sequence
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.sequences 
                            WHERE sequence_name = 'Questions_QuestionId_seq'
                        ) THEN
                            CREATE SEQUENCE ""Questions_QuestionId_seq"" START WITH 1 INCREMENT BY 1;
                            ALTER TABLE ""Questions""
                            ALTER COLUMN ""QuestionId"" SET DEFAULT nextval('""Questions_QuestionId_seq""');
                        END IF;

                        -- Lobbies.LobbyId sequence
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.sequences 
                            WHERE sequence_name = 'Lobbies_LobbyId_seq'
                        ) THEN
                            CREATE SEQUENCE ""Lobbies_LobbyId_seq"" START WITH 1 INCREMENT BY 1;
                            ALTER TABLE ""Lobbies""
                            ALTER COLUMN ""LobbyId"" SET DEFAULT nextval('""Lobbies_LobbyId_seq""');
                        END IF;
                    END $$;
                ", suppressTransaction: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    DO $$
                    BEGIN
                        -- Drop all defaults first
                        ALTER TABLE ""Users"" ALTER COLUMN ""UserId"" DROP DEFAULT;
                        ALTER TABLE ""Quizzes"" ALTER COLUMN ""QuizId"" DROP DEFAULT;
                        ALTER TABLE ""Questions"" ALTER COLUMN ""QuestionId"" DROP DEFAULT;
                        ALTER TABLE ""Lobbies"" ALTER COLUMN ""LobbyId"" DROP DEFAULT;

                        -- Drop all sequences
                        DROP SEQUENCE IF EXISTS ""Users_UserId_seq"" CASCADE;
                        DROP SEQUENCE IF EXISTS ""Quizzes_QuizId_seq"" CASCADE;
                        DROP SEQUENCE IF EXISTS ""Questions_QuestionId_seq"" CASCADE;
                        DROP SEQUENCE IF EXISTS ""Lobbies_LobbyId_seq"" CASCADE;
                    END $$;
                ", suppressTransaction: true);
            }
        }
    }
}