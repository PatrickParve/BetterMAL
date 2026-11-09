using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddThisOrThatRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThisOrThatRuns",
                columns: table => new
                {
                    Score = table.Column<int>(type: "integer", nullable: false),
                    QueueIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    AnswerChallengerIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    AnswerOtherIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    AnswerOutcomes = table.Column<List<int>>(type: "integer[]", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThisOrThatRuns", x => x.Score);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThisOrThatRuns");
        }
    }
}
