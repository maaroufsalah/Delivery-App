using Delivery.Api.Auth;
using Delivery.Application.Providers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`provider.controller.ts` : un prestataire de livraison rapporte les statuts de ses colis, par jeton.</summary>
[Route("provider/parcels")]
[Public]
[Guards(Guard.ProviderToken, Guard.NotMerchant)]
public sealed class ProviderController(ISender sender) : ApiControllerBase(sender)
{
    [HttpPost("{code}/status")]
    [RequirePermission("parcels.status")]
    public Task<IActionResult> Report(string code, [FromBody] ProviderStatusCommand? body, [FromHeader(Name = "idempotency-key")] string? idempotencyKey) =>
        Ok((body ?? new ProviderStatusCommand()) with
        {
            Code = Uri.UnescapeDataString(code), EventKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            Provider = HttpContext.Items[AccessFilter.ProviderItemKey] as ProviderContext,
        });
}
