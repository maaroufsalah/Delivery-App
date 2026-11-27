using Delivery.Domain.Parcels;

namespace Delivery.UnitTests.Domain;

/// <summary>Un colis à retenter repart en livraison, comme dans Wimo Laravel.</summary>
public sealed class RedeliveryRulesTests
{
    [Fact]
    public void A_parcel_received_at_the_warehouse_stays_eligible() => StatusCatalog.IsRouteEligible("delivery", 3).ShouldBeTrue();

    [Theory]
    [InlineData(9)]
    [InlineData(11)]
    [InlineData(14)]
    [InlineData(25)]
    [InlineData(26)]
    [InlineData(32)]
    [InlineData(35)]
    public void A_retry_status_is_eligible_for_a_delivery_route_and_dispatches_to_13(int statusId)
    {
        StatusCatalog.IsRouteEligible("delivery", statusId).ShouldBeTrue();
        StatusCatalog.RouteDispatchPath("delivery", statusId).ShouldBe([13]);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(20)]
    [InlineData(23)]
    [InlineData(27)]
    public void Delivered_refused_returning_or_cancelled_never_leaves_for_delivery(int statusId) =>
        StatusCatalog.IsRouteEligible("delivery", statusId).ShouldBeFalse();
}
