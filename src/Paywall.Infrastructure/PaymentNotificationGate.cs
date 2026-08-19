namespace Paywall.Infrastructure;

public sealed class PaymentNotificationGate : IDisposable
{
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    public async Task<T> EnterAsync<T>(Func<Task<T>> handle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handle);

        await _oneAtATime.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await handle().ConfigureAwait(false);
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    public void Dispose() => _oneAtATime.Dispose();
}
