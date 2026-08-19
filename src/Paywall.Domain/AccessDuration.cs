namespace Paywall.Domain;

public readonly record struct AccessDuration
{
    private AccessDuration(int? days) => Days = days;

    public int? Days { get; }

    public bool IsLifetime => Days is null;

    public static AccessDuration Lifetime { get; } = new(null);

    public static AccessDuration OfDays(int days)
    {
        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Duração precisa ser positiva.");
        }

        return new AccessDuration(days);
    }

    public override string ToString() => IsLifetime ? "vitalício" : $"{Days} dia(s)";
}
