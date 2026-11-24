using Delivery.Api.Auth;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Invoices;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>Les filtres de `invoiceListQuerySchema`, communs aux factures et aux factures Pro.</summary>
public sealed class InvoiceQueryParams
{
    public string[]? Status { get; set; }
    public string[]? ClientId { get; set; }
    public string[]? AgencyId { get; set; }
    public string? UnpaidOnly { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
    public string? IssuedDay { get; set; }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>`z.coerce.boolean()` : toute chaîne non vide est vraie (« false » compris).</summary>
    public InvoiceListFilter ToFilter(ListQueryParams query, string? kind)
    {
        query.Order ??= "desc";
        return new InvoiceListFilter(query.ToQueryWithDefault(25), MultiValue.AsList(Status), MultiValue.AsList(ClientId), MultiValue.AsList(AgencyId), !string.IsNullOrEmpty(UnpaidOnly),
            Clean(From), Clean(To), Clean(IssuedDay), kind);
    }
}

/// <summary>`InvoicesController` : les factures des e-commerçants (régime normal pour le personnel).</summary>
[Route("invoices")]
public sealed class InvoicesController(ISender sender, ICurrentUser caller) : ApiControllerBase(sender)
{
    private const string Kind = "normal";

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>`ClientInvoicesService.listKind` : un vendeur lit toutes ses factures, le personnel celles du régime normal.</summary>
    private string? ListKind => caller.ClientId is not null ? null : Kind;

    [HttpGet]
    [RequirePermission("finance.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] InvoiceQueryParams filter) => Ok(new GetInvoicesQuery(filter.ToFilter(query, ListKind)));

    [HttpGet("export")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] InvoiceQueryParams filter) => FileOf(new ExportInvoicesQuery(filter.ToFilter(query, ListKind), false));

    [HttpGet("days")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Days([FromQuery] ListQueryParams query, [FromQuery] InvoiceQueryParams filter, [FromQuery] string? days) =>
        Ok(new GetInvoiceDaysQuery(filter.ToFilter(query, ListKind), days));

    [HttpGet("day-pdf")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> DayPdf([FromQuery] ListQueryParams query, [FromQuery] InvoiceQueryParams filter, [FromQuery] string? day) =>
        FileOf(new GetInvoiceDayPdfQuery(filter.ToFilter(query, ListKind), day));

    [HttpGet("billable")]
    [Guards(Guard.NotMerchant)]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Billable([FromQuery] string? clientId, [FromQuery] string? agencyId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? search) =>
        Ok(new GetBillableParcelsQuery(clientId, Clean(agencyId), Clean(from), Clean(to), Clean(search)));

    [HttpGet("bulk-candidates")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> BulkCandidates() => Ok(new GetInvoiceBulkCandidatesQuery(Kind));

    [HttpGet("{code}")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Detail(string code) => Ok(new GetInvoiceQuery(code));

    [HttpPost]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Create([FromBody] CreateInvoiceCommand? body) => Created((body ?? new CreateInvoiceCommand()) with { Kind = Kind });

    [HttpPost("monthly")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Monthly([FromBody] CreateMonthlyInvoiceCommand? body) => Created((body ?? new CreateMonthlyInvoiceCommand()) with { Kind = Kind });

    [HttpPost("bulk")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Bulk([FromBody] CreateInvoicesInBulkCommand? body) => Created((body ?? new CreateInvoicesInBulkCommand()) with { Kind = Kind });

    [HttpPost("{code}/parcels")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> AddParcels(string code, [FromBody] AddInvoiceParcelsCommand? body) => Created((body ?? new AddInvoiceParcelsCommand()) with { Code = code });

    [HttpDelete("{code}/parcels/{parcelId}")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> RemoveParcel(string code, string parcelId) => Ok(new RemoveInvoiceParcelCommand(code, parcelId));

    [HttpPost("{code}/adjustments")]
    [RequirePermission("finance.validate")]
    public Task<IActionResult> AddAdjustment(string code, [FromBody] AddInvoiceAdjustmentCommand? body) => Created((body ?? new AddInvoiceAdjustmentCommand()) with { Code = code });

    [HttpDelete("{code}/adjustments/{id}")]
    [RequirePermission("finance.validate")]
    public Task<IActionResult> RemoveAdjustment(string code, string id) => Ok(new RemoveInvoiceAdjustmentCommand(code, id));

    [HttpGet("{code}/export")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> ExportOne(string code) => FileOf(new ExportInvoiceQuery(code));

    [HttpGet("{code}/pdf")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Pdf(string code) => FileOf(new GetInvoicePdfQuery(code));

    [HttpPost("{code}/issue")]
    [RequirePermission("finance.validate")]
    public Task<IActionResult> Issue(string code, [FromBody] IssueInvoiceCommand? body) => Created((body ?? new IssueInvoiceCommand()) with { Code = code });

    [HttpDelete("{code}")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Remove(string code) => Ok(new DeleteInvoiceCommand(code));
}

/// <summary>`ProInvoicesController` : les factures des Clients Pro (TVA sur le total HT), hors vendeur.</summary>
[Route("invoices-pro")]
[Guards(Guard.NotMerchant)]
public sealed class ProInvoicesController(ISender sender) : ApiControllerBase(sender)
{
    private const string Kind = "pro";

    [HttpGet]
    [RequirePermission("finance.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] InvoiceQueryParams filter) => Ok(new GetInvoicesQuery(filter.ToFilter(query, Kind)));

    [HttpGet("export")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] InvoiceQueryParams filter) => FileOf(new ExportInvoicesQuery(filter.ToFilter(query, Kind), true));

    [HttpGet("days")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Days([FromQuery] ListQueryParams query, [FromQuery] InvoiceQueryParams filter, [FromQuery] string? days) =>
        Ok(new GetInvoiceDaysQuery(filter.ToFilter(query, Kind), days));

    [HttpGet("day-pdf")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> DayPdf([FromQuery] ListQueryParams query, [FromQuery] InvoiceQueryParams filter, [FromQuery] string? day) =>
        FileOf(new GetInvoiceDayPdfQuery(filter.ToFilter(query, Kind), day));

    [HttpGet("bulk-candidates")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> BulkCandidates() => Ok(new GetInvoiceBulkCandidatesQuery(Kind));

    [HttpPost]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Create([FromBody] CreateInvoiceCommand? body) => Created((body ?? new CreateInvoiceCommand()) with { Kind = Kind });

    [HttpPost("monthly")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Monthly([FromBody] CreateMonthlyInvoiceCommand? body) => Created((body ?? new CreateMonthlyInvoiceCommand()) with { Kind = Kind });

    [HttpPost("bulk")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Bulk([FromBody] CreateInvoicesInBulkCommand? body) => Created((body ?? new CreateInvoicesInBulkCommand()) with { Kind = Kind });
}

/// <summary>`PaymentsController` : les règlements des e-commerçants.</summary>
[Route("payments")]
public sealed class PaymentsController(ISender sender) : ApiControllerBase(sender)
{
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PaymentListFilter Filter(ListQueryParams query, string[]? method, string[]? clientId, string[]? agencyId, string? invoiceId, string? from, string? to)
    {
        query.Order ??= "desc";
        return new PaymentListFilter(query.ToQueryWithDefault(25), MultiValue.AsList(method), MultiValue.AsList(clientId), MultiValue.AsList(agencyId), Clean(invoiceId), Clean(from), Clean(to));
    }

    [HttpGet]
    [RequirePermission("finance.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string[]? method, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string? invoiceId,
        [FromQuery] string? from, [FromQuery] string? to) =>
        Ok(new GetPaymentsQuery(Filter(query, method, clientId, agencyId, invoiceId, from, to)));

    [HttpGet("export")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string[]? method, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId,
        [FromQuery] string? invoiceId, [FromQuery] string? from, [FromQuery] string? to) =>
        FileOf(new ExportPaymentsQuery(Filter(query, method, clientId, agencyId, invoiceId, from, to)));

    [HttpPost]
    [RequirePermission("finance.pay")]
    public Task<IActionResult> Create([FromBody] CreatePaymentCommand? body) => Created(body ?? new CreatePaymentCommand());

    [HttpDelete("{id}")]
    [RequirePermission("finance.pay")]
    public Task<IActionResult> Remove(string id) => Ok(new DeletePaymentCommand(id));
}

/// <summary>`TransferOrdersController` : les ordres de virement (OV-).</summary>
[Route("transfer-orders")]
public sealed class TransferOrdersController(ISender sender) : ApiControllerBase(sender)
{
    [HttpPost]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Create([FromBody] CreateTransferOrderCommand? body) => Created(body ?? new CreateTransferOrderCommand());

    [HttpGet("{code}")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Detail(string code) => Ok(new GetTransferOrderQuery(code));

    [HttpGet("{code}/pdf")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Pdf(string code) => FileOf(new GetTransferOrderPdfQuery(code));

    [HttpGet("{code}/xlsx")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Xlsx(string code) => FileOf(new GetTransferOrderXlsxQuery(code));
}
