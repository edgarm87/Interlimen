using Meridian.Domain.Common;

namespace Meridian.Domain.Entities;

public class AlertRule : BaseEntity
{
    public Guid TenantId { get; private set; }
    public string CreatedByUserId { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public AlertType Type { get; private set; }
    public AlertMetric Metric { get; private set; }
    public AlertOperator Operator { get; private set; }
    public decimal Threshold { get; private set; }

    // Optional filters
    public string? CourtCity { get; private set; }
    public string? CourtState { get; private set; }
    public Guid? JudgeId { get; private set; }
    public Guid? FacilityId { get; private set; }

    public bool IsActive { get; private set; } = true;
    public DateTime? LastTriggeredAt { get; private set; }

    public ICollection<AlertNotification> Notifications { get; private set; }
        = new List<AlertNotification>();

    private AlertRule() { }

    public static AlertRule Create(
        Guid tenantId,
        string userId,
        string name,
        AlertType type,
        AlertMetric metric,
        AlertOperator op,
        decimal threshold)
    {
        return new AlertRule
        {
            TenantId = tenantId,
            CreatedByUserId = userId,
            Name = name,
            Type = type,
            Metric = metric,
            Operator = op,
            Threshold = threshold
        };
    }

    public void Triggered()
    {
        LastTriggeredAt = DateTime.UtcNow;
        SetUpdated();
    }
}

public class AlertNotification : BaseEntity
{
    public Guid AlertRuleId { get; private set; }
    public AlertRule AlertRule { get; private set; } = default!;
    public decimal TriggerValue { get; private set; }
    public string Message { get; private set; } = default!;
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public string UserId { get; private set; } = default!;

    private AlertNotification() { }

    public static AlertNotification Create(
        Guid alertRuleId,
        string userId,
        decimal value,
        string message)
    {
        return new AlertNotification
        {
            AlertRuleId = alertRuleId,
            UserId = userId,
            TriggerValue = value,
            Message = message
        };
    }

    public void MarkRead()
    {
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}

public enum AlertType
{
    Threshold,
    DataRefresh,
    CourtBacklog
}

public enum AlertMetric
{
    CourtBacklog,
    DetentionPopulation,
    RemovalRate,
    GrantRate,
    DenialRate
}

public enum AlertOperator
{
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual
}