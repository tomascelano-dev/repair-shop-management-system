using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Billing;
using RepairShop.Domain.Billing;

namespace RepairShop.Application.Tests;

/// <summary>
/// Security audit 2026-11-23 (M-05), review of 0fe5079: the reconciliation job runs inside a BackgroundService, where an
/// exception that escapes stops the whole API. Mercado Pago not answering in time must not be one of those.
/// </summary>
public sealed class BillingReconcileTests
{
    private static Subscription Running(string preapprovalId)
    {
        var now = DateTime.UtcNow;
        var sub = Subscription.StartTrial(Guid.NewGuid(), "AR", 14, now.AddDays(-40));
        sub.ApplyProviderState(BillingProvider.MercadoPago, preapprovalId, PlanId.Basic, SubscriptionStatus.Active,
            now.AddDays(1), 29900m, "ARS", null, now.AddDays(-29), now.AddDays(-29));
        return sub;
    }

    private static (SubscriptionService Service, Mock<ISubscriptionGateway> Gateway, List<Subscription> Due) Build()
    {
        var due = new List<Subscription> { Running("pre-slow"), Running("pre-next") };
        var repository = new Mock<ISubscriptionRepository>();
        repository.Setup(r => r.ListForReconciliationAsync(BillingProvider.MercadoPago, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(due);

        var gateway = new Mock<ISubscriptionGateway>();
        gateway.Setup(g => g.IsConfigured("AR")).Returns(true);
        gateway.Setup(g => g.GetSubscriptionAsync("AR", "pre-next", It.IsAny<CancellationToken>())).ReturnsAsync((GatewaySubscriptionState?)null);

        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

        var service = new SubscriptionService(repository.Object, null!, null!, gateway.Object, null!, clock.Object, null!, null!, null!,
            Options.Create(new BillingOptions()), NullLogger<SubscriptionService>.Instance, null!);
        return (service, gateway, due);
    }

    [Fact]
    public async Task M05_a_mercado_pago_timeout_is_one_failed_subscription_not_the_end_of_the_job()
    {
        var (service, gateway, due) = Build();
        // What HttpClient throws when its own timeout fires: a TaskCanceledException, with the caller's token untouched.
        gateway.Setup(g => g.GetSubscriptionAsync("AR", "pre-slow", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 20 seconds elapsing."));
        var before = due.Select(s => (s.Status, s.CurrentPeriodEndsAtUtc, s.Plan)).ToList();

        var applied = await service.ReconcileMercadoPagoAsync(CancellationToken.None);

        applied.Should().Be(0);
        gateway.Verify(g => g.GetSubscriptionAsync("AR", "pre-next", It.IsAny<CancellationToken>()), Times.Once,
            "the subscription after the one that timed out is still read");
        due.Select(s => (s.Status, s.CurrentPeriodEndsAtUtc, s.Plan)).Should().Equal(before, "a call that failed moves nothing");
    }

    [Fact]
    public async Task M05_a_shutdown_still_stops_the_job()
    {
        var (service, gateway, _) = Build();
        using var stopping = new CancellationTokenSource();
        gateway.Setup(g => g.GetSubscriptionAsync("AR", "pre-slow", It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((_, _, ct) =>
            {
                stopping.Cancel();
                return Task.FromCanceled<GatewaySubscriptionState?>(ct);
            });

        var run = () => service.ReconcileMercadoPagoAsync(stopping.Token);

        await run.Should().ThrowAsync<OperationCanceledException>();
        gateway.Verify(g => g.GetSubscriptionAsync("AR", "pre-next", It.IsAny<CancellationToken>()), Times.Never);
    }
}
