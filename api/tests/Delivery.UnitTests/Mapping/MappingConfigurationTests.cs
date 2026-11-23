using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delivery.UnitTests.Mapping;

public sealed class MappingConfigurationTests
{
    [Fact]
    public void Every_profile_is_valid()
    {
        var configuration = new MapperConfiguration(
            cfg => cfg.AddMaps(typeof(Delivery.Application.DependencyInjection).Assembly),
            NullLoggerFactory.Instance);
        configuration.AssertConfigurationIsValid();
    }
}
