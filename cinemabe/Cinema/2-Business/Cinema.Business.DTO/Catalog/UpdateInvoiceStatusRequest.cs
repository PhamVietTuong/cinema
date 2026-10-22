using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class UpdateInvoiceStatusRequest
{
    public Guid Id { get; set; }
    public InvoiceStatus Status { get; set; }
}
