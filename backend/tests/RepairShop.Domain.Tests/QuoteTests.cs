using FluentAssertions;
using RepairShop.Domain.Common;
using RepairShop.Domain.Quotes;

namespace RepairShop.Domain.Tests;

public class QuoteTests
{
    private static readonly DateTime T0 = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Quote Draft(decimal discount = 0)
    {
        var q = new Quote(Guid.NewGuid(), Guid.NewGuid(), 1, "ars", Guid.NewGuid(), T0);
        q.Edit("ARS", new[]
        {
            new QuoteItemInput(QuoteItemKind.Labor, "Cambio de módulo", 1, 15000),
            new QuoteItemInput(QuoteItemKind.Part, "Módulo", 2, 12500.555m),
        }, discount, 90, "Incluye vidrio", T0);
        return q;
    }

    [Fact]
    public void Totals_are_rounded_per_line_and_discount_applied()
    {
        var q = Draft(1000);
        q.Subtotal.Should().Be(15000 + 25001.12m); // unit price rounded first (12500.56)
        q.Total.Should().Be(q.Subtotal - 1000);
        q.Currency.Should().Be("ARS");
    }

    [Fact]
    public void Discount_cannot_exceed_subtotal()
        => ((Action)(() => Draft(1_000_000))).Should().Throw<DomainException>();

    [Fact]
    public void Customer_can_decide_only_on_sent_quotes()
    {
        var q = Draft();
        var act = () => q.Approve(QuoteDecisionSource.CustomerPortal, null, null, "1.2.3.4", T0);
        act.Should().Throw<DomainException>();

        q.Send(T0.AddDays(7), T0);
        q.Approve(QuoteDecisionSource.CustomerPortal, null, "ok", "1.2.3.4", T0.AddDays(1));
        q.Status.Should().Be(QuoteStatus.Approved);
        q.DecisionIp.Should().Be("1.2.3.4");
    }

    [Fact]
    public void Staff_can_record_approval_of_a_draft()
    {
        var q = Draft();
        q.Approve(QuoteDecisionSource.Staff, Guid.NewGuid(), "aprobó por teléfono", null, T0);
        q.Status.Should().Be(QuoteStatus.Approved);
    }

    [Fact]
    public void Expired_quotes_cannot_be_approved()
    {
        var q = Draft();
        q.Send(T0.AddDays(2), T0);
        q.ExpireIfDue(T0.AddDays(1)).Should().BeFalse();
        q.ExpireIfDue(T0.AddDays(3)).Should().BeTrue();
        q.Status.Should().Be(QuoteStatus.Expired);
        ((Action)(() => q.Approve(QuoteDecisionSource.Staff, Guid.NewGuid(), null, null, T0.AddDays(3)))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Only_drafts_are_editable_and_decisions_are_final()
    {
        var q = Draft();
        q.Send(T0.AddDays(2), T0);
        ((Action)(() => q.Edit("ARS", Array.Empty<QuoteItemInput>(), 0, null, null, T0))).Should().Throw<DomainException>();
        q.Reject(QuoteDecisionSource.Staff, Guid.NewGuid(), "caro", null, T0);
        ((Action)(() => q.Approve(QuoteDecisionSource.Staff, Guid.NewGuid(), null, null, T0))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Empty_quote_cannot_be_sent()
    {
        var q = new Quote(Guid.NewGuid(), Guid.NewGuid(), 1, "ARS", Guid.NewGuid(), T0);
        ((Action)(() => q.Send(T0.AddDays(1), T0))).Should().Throw<DomainException>();
    }
}
