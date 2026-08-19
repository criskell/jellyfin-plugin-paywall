using Paywall.Domain;
using Xunit;

namespace Paywall.Domain.Tests;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private static Order Open() => Order.Open(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new Plan("mensal", "Mensal", Money.Of(1990), BillingMode.OneTime, AccessDuration.OfDays(30)),
        "manual-pix",
        Now);

    [Fact]
    public void ReentregaDeWebhookNaoConfirmaDuasVezes()
    {
        var order = Open();

        Assert.True(order.TrySettle(Now));
        Assert.False(order.TrySettle(Now.AddMinutes(1)));
    }

    [Fact]
    public void PedidoJaPagoNaoVoltaAFalhar()
    {
        var order = Open();
        order.TrySettle(Now);

        order.Fail(Now.AddMinutes(1));

        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void EstornoMarcaOPedidoComoDevolvido()
    {
        var order = Open();
        order.TrySettle(Now);

        order.Refund(Now.AddDays(1));

        Assert.Equal(OrderStatus.Refunded, order.Status);
    }
}
