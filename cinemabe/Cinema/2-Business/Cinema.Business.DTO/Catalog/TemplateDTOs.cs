using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class TemplateDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TemplateChannel Channel { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastUpdatedTime { get; set; }
}

public class CreateTemplateRequest
{
    [Required]
    [StringLength(100)]
    public string Code { get; set; } = string.Empty;
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    public TemplateChannel Channel { get; set; } = TemplateChannel.Email;
    [StringLength(500)]
    public string? Subject { get; set; }
    [Required]
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

// Code is immutable after creation — it is the lookup key used by the sending code.
public class UpdateTemplateRequest : IHasId
{
    public Guid Id { get; set; }
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    public TemplateChannel Channel { get; set; } = TemplateChannel.Email;
    [StringLength(500)]
    public string? Subject { get; set; }
    [Required]
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
