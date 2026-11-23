using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Delivery.Api.Http;

/// <summary>Équivalent de `app.setGlobalPrefix('api')`.</summary>
public sealed class ApiRoutePrefixConvention(string prefix) : IApplicationModelConvention
{
    private readonly AttributeRouteModel _prefix = new(new Microsoft.AspNetCore.Mvc.RouteAttribute(prefix));

    public void Apply(ApplicationModel application)
    {
        foreach (var selector in application.Controllers.SelectMany(c => c.Selectors))
        {
            selector.AttributeRouteModel = selector.AttributeRouteModel is null
                ? _prefix
                : AttributeRouteModel.CombineAttributeRouteModel(_prefix, selector.AttributeRouteModel);
        }
    }
}
