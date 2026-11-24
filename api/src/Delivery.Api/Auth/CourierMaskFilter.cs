using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Delivery.Api.Auth;

/// <summary>
/// `CourierMaskInterceptor` : pour un compte qui ne voit pas l'identité des livreurs (vendeur), toute réponse JSON
/// passe par le masque avant d'être écrite. Les autres comptes n'y passent pas.
/// </summary>
public sealed class CourierMaskFilter(ICurrentUser caller, IAppDbContext db, IOptions<JsonOptions> json) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (caller.IsAuthenticated && !caller.CanSeeCourierIdentity() && context.Result is ObjectResult { Value: { } value } result && value is not FileContent)
        {
            var options = json.Value.JsonSerializerOptions;
            var node = JsonSerializer.SerializeToNode(value, value.GetType(), options);
            if (node is not null)
            {
                var mask = await CourierMasks.LoadAsync(db, caller, context.HttpContext.RequestAborted);
                result.Value = CourierMasks.Apply(node, mask);
                result.DeclaredType = null;
            }
        }
        await next();
    }
}
