using Delivery.Application.Common.Behaviors;
using Delivery.Domain.Common;

namespace Delivery.UnitTests.Domain;

public sealed class CuidTests
{
    [Fact]
    public void Looks_like_a_prisma_cuid()
    {
        var id = Cuid.New();
        id.Length.ShouldBe(25);
        id[0].ShouldBe('c');
        id.ShouldAllBe(ch => char.IsAsciiDigit(ch) || char.IsAsciiLetterLower(ch));
    }

    [Fact]
    public void Is_unique_and_sortable_over_time()
    {
        var ids = Enumerable.Range(0, 2000).Select(_ => Cuid.New()).ToList();
        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    [Theory]
    [InlineData("Items[0].Name", "items.0.name")]
    [InlineData("Email", "email")]
    [InlineData("", "")]
    public void Validation_paths_follow_zod(string input, string expected) =>
        ValidationPaths.ToPath(input).ShouldBe(expected);
}
