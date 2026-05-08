namespace BallastlaneTechStore.Domain.Common;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public sealed class OutOfStockException : DomainException
{
    public Guid ProductId { get; }
    public int Requested { get; }
    public int Available { get; }

    public OutOfStockException(Guid productId, int requested, int available)
        : base($"Insufficient stock for product {productId}: requested {requested}, available {available}.")
    {
        ProductId = productId;
        Requested = requested;
        Available = available;
    }
}
