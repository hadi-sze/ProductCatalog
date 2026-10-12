using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductCatalog.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMainCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MainCompanies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UniqCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MainCompanies", x => x.Id);
                    table.CheckConstraint("CK_MainCompanies_Address", "Address IS NULL OR length(Address) <= 500");
                    table.CheckConstraint("CK_MainCompanies_IsActive", "IsActive IN (0, 1)");
                    table.CheckConstraint("CK_MainCompanies_Name", "Name IS NULL OR length(Name) <= 200");
                    table.CheckConstraint("CK_MainCompanies_UniqCode", "UniqCode IS NULL OR length(UniqCode) BETWEEN 1 AND 64");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MainCompanies_UniqCode",
                table: "MainCompanies",
                column: "UniqCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MainCompanies");
        }
    }
}
