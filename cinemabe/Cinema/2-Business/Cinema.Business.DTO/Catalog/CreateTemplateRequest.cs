using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

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
