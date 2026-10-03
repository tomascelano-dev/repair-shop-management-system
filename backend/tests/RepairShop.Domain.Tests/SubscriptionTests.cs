using FluentAssertions;
using RepairShop.Domain.Billing;
using RepairShop.Domain.Common;

namespace RepairShop.Domain.Tests;

public sealed class SubscriptionTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Org = Guid.NewGuid();

    [Fact]
    public void Trial_gives_every_module_until_it_ends_then_the_account_is_read_only()
    {
        var sub = Subscription.StartTrial(Org, "mx", 14, Now);

        sub.Plan.Should().Be(PlanId.Pro);
        sub.BillingCountry.Should().Be("MX");
        sub.HasFullAccess(Now.AddDays(13)).Should().BeTrue();
        sub.TrialDaysLeft(Now.AddDays(13).AddHours(1)).Should().Be(1);
        sub.EffectiveStatus(Now.AddDays(14)).Should().Be(SubscriptionStatus.Expired);
        sub.HasFullAccess(Now.AddDays(14)).Should().BeFalse();
    }

    [Fact]
    public void Paid_subscription_keeps_access_during_the_grace_period_after_a_missed_renewal()
    {
        var sub = Subscription.StartTrial(Org, "AR", 14, Now);
        var periodEnd = Now.AddMonths(1);
        sub.ApplyProviderState(BillingProvider.MercadoPago, "pre1", PlanId.Standard, SubscriptionStatus.Active, periodEnd, 44900m, "ARS", null, Now, Now);

        sub.EffectiveStatus(periodEnd.AddDays(-1)).Should().Be(SubscriptionStatus.Active);
        sub.EffectiveStatus(periodEnd.AddDays(2)).Should().Be(SubscriptionStatus.PastDue);
        sub.HasFullAccess(periodEnd.AddDays(2)).Should().BeTrue();
        sub.EffectiveStatus(periodEnd + Subscription.GracePeriod).Should().Be(SubscriptionStatus.Expired);
    }

    [Fact]
    public void Canceled_subscription_works_until_the_end_of_the_paid_period()
    {
        var sub = Subscription.StartTrial(Org, "AR", 14, Now);
        var periodEnd = Now.AddMonths(1);
        sub.ApplyProviderState(BillingProvider.Paddle, "sub_1", PlanId.Basic, SubscriptionStatus.Active, periodEnd, 25m, "USD", "ctm_1", Now, Now);
        sub.MarkCanceled(Now.AddDays(3));

        sub.HasFullAccess(periodEnd.AddMinutes(-1)).Should().BeTrue();
        sub.EffectiveStatus(periodEnd).Should().Be(SubscriptionStatus.Expired);
        sub.CanceledAtUtc.Should().Be(Now.AddDays(3));
    }

    [Fact]
    public void Complimentary_plan_never_expires()
    {
        var sub = Subscription.Complimentary(Org, PlanId.Pro, "AR", Now);
        sub.HasFullAccess(Now.AddYears(5)).Should().BeTrue();
        sub.Provider.Should().Be(BillingProvider.Manual);
    }

    [Fact]
    public void Out_of_order_provider_events_are_ignored()
    {
        var sub = Subscription.StartTrial(Org, "AR", 14, Now);
        sub.ApplyProviderState(BillingProvider.Paddle, "sub_1", PlanId.Pro, SubscriptionStatus.Canceled, Now.AddMonths(1), null, null, null, Now.AddMinutes(5), Now)
            .Should().BeTrue();

        var applied = sub.ApplyProviderState(BillingProvider.Paddle, "sub_1", PlanId.Pro, SubscriptionStatus.Active, Now.AddMonths(1), null, null, null, Now, Now);

        applied.Should().BeFalse();
        sub.Status.Should().Be(SubscriptionStatus.Canceled);
    }

    [Fact]
    public void An_old_canceled_subscription_does_not_override_the_running_one()
    {
        var sub = Subscription.StartTrial(Org, "AR", 14, Now);
        sub.ApplyProviderState(BillingProvider.MercadoPago, "new", PlanId.Pro, SubscriptionStatus.Active, Now.AddMonths(1), null, null, null, Now, Now);

        sub.ApplyProviderState(BillingProvider.MercadoPago, "old", PlanId.Basic, SubscriptionStatus.Canceled, null, null, null, null, Now.AddMinutes(1), Now)
            .Should().BeFalse();
        sub.ProviderSubscriptionId.Should().Be("new");
        sub.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public void Activation_without_a_plan_uses_the_plan_chosen_at_checkout()
    {
        var sub = Subscription.StartTrial(Org, "AR", 14, Now);
        sub.BeginCheckout(PlanId.Standard, BillingProvider.MercadoPago, "pre1", Now);

        sub.ApplyProviderState(BillingProvider.MercadoPago, "pre1", null, SubscriptionStatus.Active, Now.AddMonths(1), null, null, null, Now.AddMinutes(1), Now);

        sub.Plan.Should().Be(PlanId.Standard);
        sub.PendingPlan.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ARG")]
    [InlineData("1A")]
    public void Country_must_be_an_iso_code(string country)
    {
        var act = () => Subscription.StartTrial(Org, country, 14, Now);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Plans_limit_branches_and_modules()
    {
        Plans.Basic.MaxBranches.Should().Be(1);
        Plans.Basic.Includes(PlanModules.Purchasing).Should().BeFalse();
        Plans.Standard.Includes(PlanModules.Reports).Should().BeTrue();
        Plans.Standard.Includes(PlanModules.Transfers).Should().BeFalse();
        Plans.Pro.Modules.Should().BeEquivalentTo(PlanModules.All);
    }
}
