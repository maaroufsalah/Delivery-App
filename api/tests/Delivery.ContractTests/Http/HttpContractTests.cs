using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Delivery.Api.Http;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Common;

namespace Delivery.ContractTests.Http;

[Collection(ApiCollection.Name)]
public sealed class HttpContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Unknown_route_answers_the_express_404_body()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/nope?x=1");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("code").GetString().ShouldBe("NOT_FOUND");
        body.GetProperty("message").GetString().ShouldBe("Cannot GET /api/nope?x=1");
        body.TryGetProperty("details", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Tests_run_on_their_own_database()
    {
        var database = await factory.QueryDbAsync(db => Task.FromResult(db.Database.GetDbConnection().Database));
        database.ShouldNotBe("wimo_delivery");
    }

    [Fact]
    public async Task Every_response_carries_a_request_id()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/nope");
        request.Headers.Add("x-request-id", "abcdef12-3456");
        var response = await client.SendAsync(request);
        response.Headers.GetValues("x-request-id").Single().ShouldBe("abcdef12-3456");
    }

    [Theory]
    [InlineData(ErrorCode.NOT_FOUND, 404)]
    [InlineData(ErrorCode.CONFLICT, 409)]
    [InlineData(ErrorCode.VALIDATION_FAILED, 422)]
    [InlineData(ErrorCode.TRANSITION_FORBIDDEN, 422)]
    [InlineData(ErrorCode.UNAUTHENTICATED, 401)]
    [InlineData(ErrorCode.FORBIDDEN, 403)]
    [InlineData(ErrorCode.PAYLOAD_TOO_LARGE, 413)]
    [InlineData(ErrorCode.TOO_MANY_REQUESTS, 429)]
    public void Business_codes_map_to_nest_statuses(ErrorCode code, int status)
    {
        var (actual, body) = ErrorHandlingMiddleware.Describe(new BusinessException(code, "x"));
        actual.ShouldBe(status);
        body.Code.ShouldBe(code.ToString());
    }

    [Fact]
    public void Unknown_exception_is_an_internal_error()
    {
        var (status, body) = ErrorHandlingMiddleware.Describe(new InvalidOperationException("boom"));
        status.ShouldBe(500);
        body.Message.ShouldBe("Erreur interne.");
    }
}
