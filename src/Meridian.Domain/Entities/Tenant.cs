using Meridian.Domain.Common;

namespace Meridian.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? LogoUrl { get; private set; }
    public SubscriptionTier Tier { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public int MaxSeats { get; private set; }
    public DateTime? SubscriptionExpiresAt { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public string? StripeSubscriptionId { get; private set; }

    private Tenant() { }

    public static Tenant Create(
        string name,
        string slug,
        SubscriptionTier tier,
        int maxSeats)
    {
        return new Tenant
        {
            Name = name,
            Slug = slug,
            Tier = tier,
            MaxSeats = maxSeats,
            Status = SubscriptionStatus.Trialing
        };
    }

    public void Activate(
        string stripeCustomerId,
        string stripeSubscriptionId,
        DateTime expiresAt)
    {
        StripeCustomerId = stripeCustomerId;
        StripeSubscriptionId = stripeSubscriptionId;
        SubscriptionExpiresAt = expiresAt;
        Status = SubscriptionStatus.Active;
        SetUpdated();
    }

    public void StartTrial(int days = 14)
    {
        SubscriptionExpiresAt = DateTime.UtcNow.AddDays(days);
        Status = SubscriptionStatus.Trialing;
        SetUpdated();
    }

    public bool CanAddSeat(int currentSeatCount) => currentSeatCount < MaxSeats;
}

public enum SubscriptionTier
{
    Starter,
    Pro,
    Team
}

public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    Cancelled,
    Suspended
}