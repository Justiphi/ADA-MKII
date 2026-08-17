using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ADA_MKII_Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountsAndPerUserData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Settings",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_DeviceTokens_Name",
                table: "DeviceTokens");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_UpdatedUtc",
                table: "Conversations");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "Settings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "DeviceTokens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_Settings",
                table: "Settings",
                columns: new[] { "AccountId", "Key" });

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DisabledUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_AccountId",
                table: "DeviceTokens",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AccountId_UpdatedUtc",
                table: "Conversations",
                columns: new[] { "AccountId", "UpdatedUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Username",
                table: "Accounts",
                column: "Username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Accounts_AccountId",
                table: "Conversations",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceTokens_Accounts_AccountId",
                table: "DeviceTokens",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Settings_Accounts_AccountId",
                table: "Settings",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Accounts_AccountId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceTokens_Accounts_AccountId",
                table: "DeviceTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Settings_Accounts_AccountId",
                table: "Settings");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Settings",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_DeviceTokens_AccountId",
                table: "DeviceTokens");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_AccountId_UpdatedUtc",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "DeviceTokens");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Conversations");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Settings",
                table: "Settings",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_Name",
                table: "DeviceTokens",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UpdatedUtc",
                table: "Conversations",
                column: "UpdatedUtc",
                descending: new bool[0]);
        }
    }
}
