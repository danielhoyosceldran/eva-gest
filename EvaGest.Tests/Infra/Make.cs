using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.Tests.Infra;

/// <summary>Fluent builders so each test states only what it actually cares about.</summary>
public static class Make
{
    public static Client Client(string name = "Joan García", string mobile = "612345678")
        => new() { Name = name, Mobile = mobile, ClientKey = ClientService.ComputeClientKey(name) };

    public static Service Service(string name = "Tall", int priceCents = 1500,
                                int vatBp = 2100, int? duration = 30)
        => new() { Name = name, PriceCents = priceCents, VatBp = vatBp,
                   DurationMin = duration, Active = true };

    public static Product Product(string name = "Cera", int priceCents = 900, int vatBp = 2100)
        => new() { Name = name, PriceCents = priceCents, VatBp = vatBp, Active = true };

    public static Worker Worker(string name = "Marta", bool active = true)
        => new() { Name = name, Active = active, Color = "#0F766E" };

    public static PaymentMethod Method(string name = "Efectiu")
        => new() { Name = name, Active = true };

    public static SaleLine Line(int amountCents, int vatBp = 2100, int quantity = 1)
        => new() { Description = "Prova", Quantity = quantity,
                   UnitPriceCents = amountCents / quantity,
                   VatBp = vatBp, AmountCents = amountCents };

    /// <summary>Builds a sale together with its frozen VAT breakdown, mirroring what
    /// SaleService will do once it exists (decision 6.4: never recompute from lines).</summary>
    public static Sale Sale(DateOnly date, int paymentMethodId, SaleStatus status,
                              params SaleLine[] lines)
    {
        var d = EvaGest.Services.VatCalculator.Compute(lines, VatMode.Included);
        return new Sale
        {
            Date = date,
            Time = new TimeOnly(10, 0),
            GuestName = "Client de prova", // satisfies the client XOR guest check constraint
            PaymentMethodId = paymentMethodId,
            BaseCents = d.BaseCents,
            VatCents = d.VatCents,
            TotalCents = d.TotalCents,
            VatMode = VatMode.Included,
            Status = status,
            Lines = [.. lines],
            Breakdowns = EvaGest.Services.VatCalculator.ToBreakdownRows(lines, VatMode.Included)
        };
    }
}
