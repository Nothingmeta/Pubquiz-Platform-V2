using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pubquiz_Platform_V2.Migrations
{
    /// <inheritdoc />
    public partial class FixIsActiveColumnTypeForPostgreSQL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only PostgreSQL
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns 
                            WHERE table_name = 'Lobbies' 
                            AND column_name = 'IsActive' 
                            AND data_type = 'integer'
                        ) THEN
                            -- Drop the default constraint
                            ALTER TABLE ""Lobbies""
                            ALTER COLUMN ""IsActive"" DROP DEFAULT;
                            
                            -- Convert the type
                            ALTER TABLE ""Lobbies"" 
                            ALTER COLUMN ""IsActive"" TYPE boolean USING ""IsActive""::boolean;
                            
                            -- Set the new default
                            ALTER TABLE ""Lobbies""
                            ALTER COLUMN ""IsActive"" SET DEFAULT true;
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
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns 
                            WHERE table_name = 'Lobbies' 
                            AND column_name = 'IsActive' 
                            AND data_type = 'boolean'
                        ) THEN
                            ALTER TABLE ""Lobbies""
                            ALTER COLUMN ""IsActive"" DROP DEFAULT;
                            
                            ALTER TABLE ""Lobbies"" 
                            ALTER COLUMN ""IsActive"" TYPE integer USING ""IsActive""::integer;
                            
                            ALTER TABLE ""Lobbies""
                            ALTER COLUMN ""IsActive"" SET DEFAULT 1;
                        END IF;
                    END $$;
                ", suppressTransaction: true);
            }
        }
    }
}