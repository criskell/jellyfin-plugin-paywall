using Paywall.Infrastructure;
using Xunit;

namespace Paywall.Tests;

public class PaymentNotificationGateTests
{
    [Fact]
    public async Task NotificacoesSimultaneasNaoSeAtropelam()
    {
        using var gate = new PaymentNotificationGate();
        var applied = 0;

        async Task<int> ApplyOnce()
        {
            var seen = applied;
            await Task.Yield();
            applied = seen + 1;
            return applied;
        }

        await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => gate.EnterAsync(ApplyOnce, CancellationToken.None)));

        Assert.Equal(50, applied);
    }

    [Fact]
    public async Task FalhaDeUmaNotificacaoNaoTravaAsSeguintes()
    {
        using var gate = new PaymentNotificationGate();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.EnterAsync<int>(() => throw new InvalidOperationException(), CancellationToken.None));

        Assert.Equal(1, await gate.EnterAsync(() => Task.FromResult(1), CancellationToken.None));
    }
}
