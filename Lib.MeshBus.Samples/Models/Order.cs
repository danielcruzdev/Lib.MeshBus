namespace Lib.MeshBus.Samples.Models;

public class Order
{
    public int Id { get; set; }
    public string Product { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public override string ToString() => $"#{Id:D3} | {Product,-20} | {Amount,8:C}";
}
