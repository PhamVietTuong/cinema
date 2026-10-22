using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.BoxOffice;

public class FindCustomerRequest
{
    [Required]
    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;
}
