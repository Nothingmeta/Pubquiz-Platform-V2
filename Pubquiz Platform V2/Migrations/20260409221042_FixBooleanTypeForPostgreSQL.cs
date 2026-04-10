using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pubquiz_Platform_V2.Migrations
{
    /// <inheritdoc />
    public partial class FixBooleanTypeForPostgreSQL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only PostgreSQL has this schema information
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns 
                            WHERE table_name = 'Users' 
                            AND column_name = 'IsTwoFactorEnabled' 
                            AND data_type = 'integer'
                        ) THEN
                            -- First, drop the default constraint
                            ALTER TABLE ""Users""
                            ALTER COLUMN ""IsTwoFactorEnabled"" DROP DEFAULT;
                            
                            -- Now convert the type
                            ALTER TABLE ""Users"" 
                            ALTER COLUMN ""IsTwoFactorEnabled"" TYPE boolean USING ""IsTwoFactorEnabled""::boolean;
                            
                            -- Set the new boolean default
                            ALTER TABLE ""Users""
                            ALTER COLUMN ""IsTwoFactorEnabled"" SET DEFAULT false;
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
                            WHERE table_name = 'Users' 
                            AND column_name = 'IsTwoFactorEnabled' 
                            AND data_type = 'boolean'
                        ) THEN
                            -- Drop the boolean default
                            ALTER TABLE ""Users""
                            ALTER COLUMN ""IsTwoFactorEnabled"" DROP DEFAULT;
                            
                            -- Convert back to integer
                            ALTER TABLE ""Users"" 
                            ALTER COLUMN ""IsTwoFactorEnabled"" TYPE integer USING ""IsTwoFactorEnabled""::integer;
                            
                            -- Set the integer default
                            ALTER TABLE ""Users""
                            ALTER COLUMN ""IsTwoFactorEnabled"" SET DEFAULT 0;
                        END IF;
                    END $$;
                ", suppressTransaction: true);
            }
        }
    }
}