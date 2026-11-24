using Delivery.Api.Auth;
using Delivery.Application.Exchanges.Commands;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Application.Parcels.Labels;
using Delivery.Application.Parcels.Import;
using Delivery.Application.Common.Models;
using Delivery.Application.Parcels.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("parcels")]
public sealed partial class ParcelsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> List([FromQuery] ParcelListParams query) => Ok(new GetParcelsQuery(query.ToFilter()));

    [HttpGet("export")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Export([FromQuery] ParcelListParams query) => FileOf(new ExportParcelsQuery(query.ToFilter()));

    [HttpGet("stats")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Stats([FromQuery] ParcelListParams query) => Ok(new GetParcelStatsQuery(query.ToFilter()));

    [HttpGet("next-code")]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> NextCode([FromQuery] GetNextParcelCodeQuery query) => Ok(query);

    [HttpPost("quote")]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> Quote([FromBody] QuoteParcelQuery? body) => Created(body ?? new QuoteParcelQuery());

    [HttpPost]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> Create([FromBody] CreateParcelCommand? body, [FromHeader(Name = "idempotency-key")] string? idempotencyKey) =>
        Created((body ?? new CreateParcelCommand()) with { IdempotencyKey = idempotencyKey });

    [HttpGet("import/template.xlsx")]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> ImportTemplate() => FileOf(new GetImportTemplateQuery());

    [HttpPost("import/preview")]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> PreviewImport([FromBody] PreviewParcelImportCommand? body) => Created(body ?? new PreviewParcelImportCommand());

    [HttpPost("import/anomalies.xlsx")]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> ImportAnomalies([FromBody] ExportImportAnomaliesCommand? body) => FileOf(body ?? new ExportImportAnomaliesCommand());

    [HttpPost("import")]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> RunImport([FromBody] RunParcelImportCommand? body) => Created(body ?? new RunParcelImportCommand());

    [HttpGet("imports")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> ImportHistory([FromQuery] ListQueryParams query, [FromQuery] string? clientId) =>
        Ok(new GetParcelImportsQuery(query.ToQueryWithDefault(20), clientId));

    [HttpGet("imports/{id}/report.xlsx")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> ImportReport(string id) => FileOf(new GetParcelImportReportQuery(id));

    [HttpGet("labels")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> LabelBatch([FromQuery] GetLabelBatchQuery query) => FileOf(query);

    [HttpPost("labels")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> LabelSelection([FromBody] PrintLabelSelectionCommand? body) => FileOf(body ?? new PrintLabelSelectionCommand());

    [HttpPost("label-jobs")]
    [RequirePermission("parcels.read")]
    public async Task<IActionResult> CreateLabelJob([FromBody] CreateLabelJobCommand? body) =>
        StatusCode(StatusCodes.Status202Accepted, await Sender.Send(body ?? new CreateLabelJobCommand(), HttpContext.RequestAborted));

    [HttpGet("label-jobs")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> LabelJobs()
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new GetLabelJobsQuery());
    }

    [HttpGet("label-jobs/{id}")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> LabelJob(string id)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new GetLabelJobQuery(id));
    }

    [HttpGet("label-jobs/{id}/file")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> LabelJobFile(string id) => FileOf(new GetLabelJobFileQuery(id));

    [HttpGet("{code}/label")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Label(string code, [FromQuery] string? format) => FileOf(new GetParcelLabelQuery { Code = code, Format = format ?? "10x10" });

    [HttpGet("{code}")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Detail(string code, [FromQuery] string? deleted) => Ok(new GetParcelQuery(code, deleted is "1" or "true"));

    [HttpPost("{code}/events")]
    [RequirePermission("parcels.status")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> ChangeStatus(string code, [FromBody] ChangeParcelStatusCommand? body) =>
        Created((body ?? new ChangeParcelStatusCommand()) with { Code = code });

    [HttpPatch("{code}/fees")]
    [RequirePermission("parcels.editFees")]
    public Task<IActionResult> EditFees(string code, [FromBody] EditParcelFeesCommand? body) => Ok((body ?? new EditParcelFeesCommand()) with { Code = code });

    [HttpPatch("{code}")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.Merchant)]
    public Task<IActionResult> UpdateBySeller(string code, [FromBody] UpdateParcelBySellerCommand? body) => Ok((body ?? new UpdateParcelBySellerCommand()) with { Code = code });

    [HttpDelete("{code}")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.Merchant)]
    public Task<IActionResult> RemoveBySeller(string code) => Ok(new RemoveParcelBySellerCommand(code));

    [HttpPatch("{code}/expected-payment")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> ChangeExpectedPayment(string code, [FromBody] ChangeExpectedPaymentCommand? body) => Ok((body ?? new ChangeExpectedPaymentCommand()) with { Code = code });

    [HttpPatch("{code}/bl")]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> SetBlRequested(string code, [FromBody] SetParcelBlRequestedCommand? body) => Ok((body ?? new SetParcelBlRequestedCommand()) with { Code = code });

    [HttpPost("{code}/notes")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> AddNote(string code, [FromBody] AddParcelNoteCommand? body) => Created((body ?? new AddParcelNoteCommand()) with { Code = code });

    [HttpPost("{code}/admin-delete")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> AdminDelete(string code, [FromBody] AdminDeleteParcelCommand? body) => Ok((body ?? new AdminDeleteParcelCommand()) with { Code = code });

    [HttpPost("{code}/client/preview")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> PreviewClientChange(string code, [FromBody] PreviewParcelClientChangeCommand? body) => Ok((body ?? new PreviewParcelClientChangeCommand()) with { Code = code });

    [HttpPost("{code}/client")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> ChangeClient(string code, [FromBody] ChangeParcelClientCommand? body) => Ok((body ?? new ChangeParcelClientCommand()) with { Code = code });

    [HttpPatch("{code}/destination")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> ChangeDestination(string code, [FromBody] ChangeParcelDestinationCommand? body) => Ok((body ?? new ChangeParcelDestinationCommand()) with { Code = code });

    [HttpPost("{code}/exchange")]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> RequestExchange(string code, [FromBody] RequestParcelExchangeCommand? body) => Created((body ?? new RequestParcelExchangeCommand()) with { Code = code });
}
