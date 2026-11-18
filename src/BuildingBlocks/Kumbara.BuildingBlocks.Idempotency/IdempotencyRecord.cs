namespace Kumbara.BuildingBlocks.Idempotency;

public class IdempotencyRecord
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
