using BallastlaneTechStore.Domain.Common;
using BallastlaneTechStore.Domain.Enums;

namespace BallastlaneTechStore.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; private set; }
    public string Company { get; private set; } = default!;
    public string ContactName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string? Phone { get; private set; }
    public CustomerStatus Status { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Customer() { }

    public static Customer Create(string company, string contactName, string email, string? phone, Guid ownerId, DateTime nowUtc)
    {
        Guard(company, contactName, email, ownerId);
        return new Customer
        {
            Id = Guid.NewGuid(),
            Company = company.Trim(),
            ContactName = contactName.Trim(),
            Email = User.NormaliseEmail(email),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            Status = CustomerStatus.Lead,
            OwnerId = ownerId,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
        };
    }

    public static Customer Hydrate(Guid id, string company, string contactName, string email, string? phone,
        CustomerStatus status, Guid ownerId, DateTime createdAt, DateTime updatedAt)
        => new()
        {
            Id = id, Company = company, ContactName = contactName, Email = email, Phone = phone,
            Status = status, OwnerId = ownerId, CreatedAt = createdAt, UpdatedAt = updatedAt,
        };

    public void Update(string company, string contactName, string email, string? phone, DateTime nowUtc)
    {
        Guard(company, contactName, email, OwnerId);
        Company = company.Trim();
        ContactName = contactName.Trim();
        Email = User.NormaliseEmail(email);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        UpdatedAt = nowUtc;
    }

    // Forward-only Lead → Prospect → Active. Anything → Churned. No reviving Churned.
    public void PromoteTo(CustomerStatus next, DateTime nowUtc)
    {
        if (next == Status) return;
        if (Status == CustomerStatus.Churned) throw new DomainException("Churned customer cannot be revived.");
        if (next == CustomerStatus.Churned) { Status = next; UpdatedAt = nowUtc; return; }
        if ((int)next <= (int)Status) throw new DomainException($"Cannot move customer back from {Status} to {next}.");
        Status = next;
        UpdatedAt = nowUtc;
    }

    private static void Guard(string company, string contactName, string email, Guid ownerId)
    {
        if (string.IsNullOrWhiteSpace(company)) throw new DomainException("Company is required.");
        if (string.IsNullOrWhiteSpace(contactName)) throw new DomainException("Contact name is required.");
        if (string.IsNullOrWhiteSpace(email)) throw new DomainException("Email is required.");
        if (ownerId == Guid.Empty) throw new DomainException("Owner is required.");
    }
}
