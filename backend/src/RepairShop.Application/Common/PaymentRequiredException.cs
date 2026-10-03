namespace RepairShop.Application.Common;

/// <summary>402: the subscription is inactive or the plan does not include the feature.</summary>
public class PaymentRequiredException : Exception
{
    public const string SubscriptionInactive = "subscription_inactive";
    public const string PlanUpgradeRequired = "plan_upgrade_required";

    public PaymentRequiredException(string code, string message, string? module = null) : base(message)
    {
        Code = code;
        Module = module;
    }

    public string Code { get; }

    /// <summary>Module the plan lacks (for <see cref="PlanUpgradeRequired"/>).</summary>
    public string? Module { get; }
}
