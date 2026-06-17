using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CS2M.ApiServer.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "servers",
                columns: table => new
                {
                    token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    local_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    local_port = table.Column<int>(type: "integer", nullable: false),
                    public_endpoint = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    last_heartbeat_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servers", x => x.token);
                });

            migrationBuilder.CreateTable(
                name: "port_check_results",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<short>(type: "smallint", nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_port_check_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_port_check_results_servers_token",
                        column: x => x.token,
                        principalTable: "servers",
                        principalColumn: "token",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_port_check_results_token_checked_at",
                table: "port_check_results",
                columns: new[] { "token", "checked_at" });

            migrationBuilder.CreateIndex(
                name: "IX_servers_is_public",
                table: "servers",
                column: "is_public");

            migrationBuilder.CreateIndex(
                name: "IX_servers_last_heartbeat_at",
                table: "servers",
                column: "last_heartbeat_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "port_check_results");

            migrationBuilder.DropTable(
                name: "servers");
        }
    }
}
