using Delivery.Domain.Finance;

namespace Delivery.UnitTests.Domain;

public sealed class CourierPayTests
{
    private static readonly IReadOnlyList<CourierGridRate> Grid = [new("casa", 500, 1500), new("rabat", 700, 2250)];

    [Fact]
    public void Course_outcome_follows_the_route_kind_and_final_status()
    {
        CourierPay.OutcomeOf("PICKUP", null).ShouldBe(CourseOutcome.Pickup);
        CourierPay.OutcomeOf("DELIVERY", 4).ShouldBe(CourseOutcome.Delivery);
        CourierPay.OutcomeOf("DELIVERY", 8).ShouldBe(CourseOutcome.Failure);
        CourierPay.OutcomeOf("DELIVERY", 14).ShouldBeNull();
        CourierPay.OutcomeOf("DELIVERY", null).ShouldBeNull();
    }

    [Fact]
    public void Commission_is_negotiated_then_failure_setting_then_grid_with_route_city_fallback()
    {
        CourierPay.CommissionFor(CourseOutcome.Delivery, "casa", 1234, Grid, 300, null).ShouldBe((1234L, "NEGOTIATED"));
        CourierPay.CommissionFor(CourseOutcome.Failure, "casa", null, Grid, 300, null).ShouldBe((300L, "FAILURE_SETTING"));
        CourierPay.CommissionFor(CourseOutcome.Delivery, "rabat", null, Grid, 300, null).ShouldBe((2250L, "GRID"));
        CourierPay.CommissionFor(CourseOutcome.Pickup, "rabat", null, Grid, 300, null).ShouldBe((700L, "GRID"));
        CourierPay.CommissionFor(CourseOutcome.Delivery, "fes", null, Grid, 300, "casa").ShouldBe((1500L, "GRID"));
        CourierPay.CommissionFor(CourseOutcome.Delivery, "fes", null, Grid, 300, null).ShouldBe((0L, "GRID"));
    }

    [Fact]
    public void A_cancelled_cheque_counts_only_what_the_remittance_kept()
    {
        CourierPay.ChequePaidOn(null, false, null).ShouldBe(0);
        CourierPay.ChequePaidOn(15050, false, 0).ShouldBe(15050);
        CourierPay.ChequePaidOn(15050, true, null).ShouldBe(15050);
        CourierPay.ChequePaidOn(15050, true, 5000).ShouldBe(5000);
        CourierPay.ChequePaidOn(15050, true, -10).ShouldBe(0);
        CourierPay.ChequePaidOn(15050, true, 99999).ShouldBe(15050);
    }
}
