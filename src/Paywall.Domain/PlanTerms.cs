namespace Paywall.Domain;

public sealed record PlanTerms
{
    private PlanTerms(string planId, Money price, AccessDuration duration)
    {
        if (string.IsNullOrWhiteSpace(planId))
        {
            throw new ArgumentException("Condições precisam do plano.", nameof(planId));
        }

        PlanId = planId;
        Price = price;
        Duration = duration;
    }

    public string PlanId { get; }

    public Money Price { get; }

    public AccessDuration Duration { get; }

    public static PlanTerms Of(Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new PlanTerms(plan.Id, plan.Price, plan.Duration);
    }

    public static PlanTerms Restore(string planId, Money price, AccessDuration duration) =>
        new(planId, price, duration);
}
