using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorPortfolio.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistryFieldsToProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RepoUrl",
                table: "Projects",
                newName: "SolutionOverview");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Projects",
                newName: "Status");

            migrationBuilder.AlterColumn<string>(
                name: "TechStack",
                table: "Projects",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Projects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DemoUrl",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetailedDescription",
                table: "Projects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Featured",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "KeyFeatures",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProblemStatement",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "RepositoryUrl",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                table: "Projects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Projects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE \"Projects\" SET \"Slug\" = 'project-' || \"Id\" WHERE \"Slug\" = '' OR \"Slug\" IS NULL;");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Category", "DemoUrl", "DetailedDescription", "Featured", "ImageUrl", "IsPublished", "KeyFeatures", "LiveUrl", "ProblemStatement", "PublishedAt", "RepositoryUrl", "ShortDescription", "Slug", "SolutionOverview", "Status", "UpdatedAt" },
                values: new object[] { "Full Stack", null, "This personal developer portfolio is a custom-built CMS application created using Blazor Server and ASP.NET Core, with Entity Framework Core mapping data to a Neon PostgreSQL instance. It allows administrators to update resumes, manage skills, configure site metadata, and read incoming messages securely.", false, null, true, "Interactive admin dashboard\nDynamic resume uploader\nSecurity logs & Spam protection\nMinimal API endpoints", "https://jhersonaguto.dev", "Managing personal developer details, resumes, and showcase builds across static templates often requires tedious manual code updates and lacks a dedicated dashboard.", new DateTime(2026, 6, 8, 0, 0, 0, 0, DateTimeKind.Utc), "https://github.com/Jherson-Aguto/WebApp-Portfolio", "Problem: Building a personal brand that is easy to manage. Solution: A custom-built CMS and portfolio. Tech: Blazor Server, EF Core, Neon PostgreSQL. Result: A professional, dynamic portfolio.", "portfolio-cms", "A central Blazor Server CMS application serving as the administration cockpit, storing records securely in PostgreSQL, and serving metadata dynamically.", "Live", new DateTime(2026, 6, 8, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Slug",
                table: "Projects",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_Slug",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DemoUrl",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DetailedDescription",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Featured",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "KeyFeatures",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProblemStatement",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RepositoryUrl",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Projects");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Projects",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "SolutionOverview",
                table: "Projects",
                newName: "RepoUrl");

            migrationBuilder.AlterColumn<string>(
                name: "TechStack",
                table: "Projects",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "LiveUrl", "RepoUrl" },
                values: new object[] { "Problem: Building a personal brand that is easy to manage. Solution: A custom-built CMS and portfolio. Tech: Blazor Server, EF Core, Neon PostgreSQL. Result: A professional, dynamic portfolio.", null, null });
        }
    }
}
