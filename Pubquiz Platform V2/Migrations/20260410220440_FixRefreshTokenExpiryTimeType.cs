using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pubquiz_Platform_V2.Migrations
{
    /// <inheritdoc />
    public partial class FixRefreshTokenExpiryTimeType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    ALTER TABLE ""Users""
                    ALTER COLUMN ""RefreshTokenExpiryTime"" TYPE timestamp without time zone
                    USING NULLIF(""RefreshTokenExpiryTime"", '')::timestamp;
                ", suppressTransaction: true);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    ALTER TABLE ""Users""
                    ALTER COLUMN ""RefreshTokenExpiryTime"" TYPE text
                    USING ""RefreshTokenExpiryTime""::text;
                ", suppressTransaction: true);
            }
        }
    }
}
