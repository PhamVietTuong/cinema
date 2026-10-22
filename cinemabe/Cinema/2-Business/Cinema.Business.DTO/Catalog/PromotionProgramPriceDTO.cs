using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class PromotionProgramPriceDTO
{
    /// <summary>Null = any format.</summary>
    public ProjectionForm? ProjectionForm { get; set; }
    /// <summary>Null = any seat kind.</summary>
    public SeatKind? SeatKind { get; set; }
    public double Price { get; set; }
}
