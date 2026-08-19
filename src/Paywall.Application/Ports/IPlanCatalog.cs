using Paywall.Domain;

namespace Paywall.Application.Ports;

public interface IPlanCatalog
{
    IReadOnlyCollection<Plan> All { get; }

    Plan? Find(string planId);
}
