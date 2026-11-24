using Delivery.Api.Auth;
using Delivery.Application.Routes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`parcel-moves.controller.ts` : les gestes du bureau qui déplacent un colis entre statuts et circuits.</summary>
[Route("parcels")]
[Guards(Guard.NotMerchant)]
public sealed class ParcelMovesController(ISender sender) : ApiControllerBase(sender)
{
    [HttpPost("{code}/correction/preview")]
    [RequirePermission("parcels.correctStatus")]
    public Task<IActionResult> PreviewCorrection(string code, [FromBody] PreviewStatusCorrectionCommand? body) =>
        Ok((body ?? new PreviewStatusCorrectionCommand()) with { Code = Uri.UnescapeDataString(code) });

    [HttpPost("{code}/correction")]
    [RequirePermission("parcels.correctStatus")]
    public Task<IActionResult> ApplyCorrection(string code, [FromBody] ApplyStatusCorrectionCommand? body) =>
        Ok((body ?? new ApplyStatusCorrectionCommand()) with { Code = Uri.UnescapeDataString(code) });

    [HttpPost("{code}/cancel-delivery")]
    [RequirePermission("parcels.status")]
    public Task<IActionResult> CancelDelivery(string code, [FromBody] CancelDeliveryCommand? body) =>
        Ok((body ?? new CancelDeliveryCommand()) with { Code = Uri.UnescapeDataString(code) });
}
