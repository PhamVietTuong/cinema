using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.BoxOffice;

public class ShowtimesTodayRequest
{
    public Guid? TheaterId { get; set; }

    /// <summary>Business day (server local time). Defaults to today.</summary>
    public DateTime? Date { get; set; }
}
