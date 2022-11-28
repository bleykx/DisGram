using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DisGram.Migrations
{
    public partial class updaate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Available",
                table: "StaffMembers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Available",
                table: "StaffMembers");
        }
    }
}
