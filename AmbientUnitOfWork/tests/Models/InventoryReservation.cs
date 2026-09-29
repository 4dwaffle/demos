namespace AmbientUnitOfWork.Tests.Models;

public sealed class InventoryReservation
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Sku { get; set; } = "";
}
