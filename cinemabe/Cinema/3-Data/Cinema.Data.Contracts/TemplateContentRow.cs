using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>Read-only projection of the content a sender needs to render a template (no tracking).</summary>
public sealed class TemplateContentRow
{
    public string Code { get; set; } = string.Empty;
    public TemplateChannel Channel { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
}
