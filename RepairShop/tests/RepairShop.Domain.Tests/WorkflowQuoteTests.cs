using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;
using Xunit;

namespace RepairShop.Domain.Tests;

public class WorkflowQuoteTests
{
    [Fact]
    public void Total_RoundsUnitPriceBeforeMultiplying()
    {
        Assert.Equal(30.03m, WorkflowQuote.CalculateTotal([new("Repuesto",3,10.005m,4m)]));
    }
    [Fact]
    public void Quote_CannotBeDecidedAfterExpiry()
    {
        var now=DateTime.UtcNow;
        var quote=new WorkflowQuote{ExpiresAtUtc=now};
        Assert.Throws<DomainException>(()=>quote.Decide(true,"Ana Pérez",now));
        Assert.Equal("Sent",quote.Status);
    }
    [Fact]
    public void AcceptedQuote_CannotBeChangedToRejected()
    {
        var now=DateTime.UtcNow;
        var quote=new WorkflowQuote{ExpiresAtUtc=now.AddDays(7)};
        quote.Decide(true," Ana Pérez ",now);
        Assert.Throws<DomainException>(()=>quote.Decide(false,"Otra persona",now));
        Assert.Equal("Accepted",quote.Status);
        Assert.Equal("Ana Pérez",quote.DecisionBy);
        Assert.Equal(now,quote.DecidedAtUtc);
    }
    [Fact]
    public void SupersededQuote_CannotBeApproved()
    {
        var quote=new WorkflowQuote{Status="Superseded",ExpiresAtUtc=DateTime.UtcNow.AddDays(7)};
        Assert.Throws<DomainException>(()=>quote.Decide(true,"Ana Pérez",DateTime.UtcNow));
    }
    [Theory]
    [InlineData(0,10,0)]
    [InlineData(1,-1,0)]
    [InlineData(1,10,-1)]
    [InlineData(1001,10,0)]
    public void Quote_RejectsInvalidQuantitiesAndAmounts(int count,decimal price,decimal cost)
    {
        Assert.Throws<DomainException>(()=>WorkflowQuote.CalculateTotal([new("Repuesto",count,price,cost)]));
    }
}
