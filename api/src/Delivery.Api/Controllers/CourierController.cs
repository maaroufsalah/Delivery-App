using Delivery.Api.Auth;
using Delivery.Application.Couriers.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>L'espace du livreur connecté (`/courier/me`).</summary>
[Route("courier/me")]
[Guards(Guard.Courier)]
public sealed class CourierController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Me() => Ok(new GetCourierMeQuery());
}
