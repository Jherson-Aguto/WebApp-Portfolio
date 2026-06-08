using System;
using System.Collections.Generic;

namespace BlazorPortfolio.Models;

public class LabsProjectDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string DetailedDescription { get; set; } = string.Empty;
    public string? ProblemStatement { get; set; }
    public string? SolutionOverview { get; set; }
    public List<string> KeyFeatures { get; set; } = new();
    public List<string> TechStack { get; set; } = new();
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? LiveUrl { get; set; }
    public string? RepositoryUrl { get; set; }
    public string? DemoUrl { get; set; }
    public bool Featured { get; set; }
    public int SortOrder { get; set; }
    public string PublishedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}
