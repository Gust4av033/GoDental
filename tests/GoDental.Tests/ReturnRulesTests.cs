using GoDental.Domain.Rules;
using Xunit;
namespace GoDental.Tests;
public sealed class ReturnRulesTests
{
    [Fact] public void PartialReturnsConsumeExactCents()
    {
        decimal refunded=0;
        for(var i=0;i<3;i++) refunded+=ReturnRules.Amount(3,1,i,refunded,1);
        Assert.Equal(1,refunded);
        Assert.Equal(.34m,ReturnRules.Amount(3,1,1,.33m,1));
    }
    [Fact] public void RefundUsesSaleTotalIncludingOriginalDiscountAndTax()
    {
        Assert.Equal(6.17m,ReturnRules.Amount(2,12.34m,0,0,1));
        Assert.Equal(0,ReturnRules.Amount(1,0,0,0,1));
        Assert.Equal(.01m,ReturnRules.Amount(100,.01m,99,0,1));
    }
    [Theory] [InlineData(101,40,40,61)] [InlineData(20,40,20,0)] [InlineData(20,0,0,20)]
    public void DebtIsReducedBeforeCash(decimal amount,decimal balance,decimal debt,decimal cash)
    { Assert.Equal((debt,cash),ReturnRules.Settlement(amount,balance)); }
    [Fact] public void WindowUsesElapsedTimeNotCalendarDate()
    {
        var purchase=DateTimeOffset.Parse("2026-10-21T23:59:00-06:00");
        Assert.True(ReturnRules.WithinWindow(purchase,purchase.AddHours(24),24));
        Assert.False(ReturnRules.WithinWindow(purchase,purchase.AddHours(24).AddSeconds(1),24));
        Assert.False(ReturnRules.WithinWindow(purchase,purchase.AddSeconds(-1),24));
        Assert.False(ReturnRules.WithinWindow(purchase,purchase,0));
    }
    [Fact] public void RejectsOverReturnsAndInconsistentHistory()
    {
        Assert.Throws<ArgumentException>(()=>ReturnRules.Amount(3,10,2,7,2));
        Assert.Throws<ArgumentException>(()=>ReturnRules.Amount(3,10,1,8,1));
        Assert.Throws<ArgumentException>(()=>ReturnRules.Settlement(10,-1));
    }
}
