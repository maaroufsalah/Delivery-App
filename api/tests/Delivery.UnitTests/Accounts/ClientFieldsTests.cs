using System.Text.Json.Nodes;
using Delivery.Application.Clients;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;

namespace Delivery.UnitTests.Accounts;

public sealed class ClientFieldsTests
{
    [Fact]
    public void Full_schema_requires_the_identity_fields()
    {
        var issues = new List<(string Path, string Message)>();
        ClientFields.Parse(JsonNode.Parse("{}"), partial: false, issues);
        issues.Select(i => i.Path).ShouldBe(["name", "phone", "email", "cityId"]);
    }

    [Fact]
    public void Patch_keeps_absent_fields_and_clears_empty_ones()
    {
        var issues = new List<(string Path, string Message)>();
        var parsed = ClientFields.Parse(JsonNode.Parse("""{"legalName":"","agencyId":null,"type":"company"}"""), partial: true, issues);
        issues.ShouldBeEmpty();
        var client = new Client { Name = "Atlas", LegalName = "Atlas SARL", AgencyId = "agc-1" };
        ClientFields.Apply(client, parsed);
        client.Name.ShouldBe("Atlas");
        client.LegalName.ShouldBeNull();
        client.AgencyId.ShouldBeNull();
        client.Type.ShouldBe(ClientType.COMPANY);
    }

    [Fact]
    public void Phone_is_normalized_and_validated()
    {
        var issues = new List<(string Path, string Message)>();
        var parsed = ClientFields.Parse(JsonNode.Parse("""{"phone":"00 212 6 11-22"}"""), partial: true, issues);
        parsed["phone"]!.GetValue<string>().ShouldBe("+212611" + "22");
        ClientFields.Parse(JsonNode.Parse("""{"phone":"abc"}"""), partial: true, issues);
        issues.ShouldContain(("phone", "invalid-phone"));
    }

    [Fact]
    public void Amounts_are_formatted_like_the_pdfs() => ClientRows.FormatAmount(1805).ShouldBe("18,05 MAD");
}
