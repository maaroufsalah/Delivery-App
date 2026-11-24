using Delivery.Api.Auth;
using Delivery.Application.Audit;
using Delivery.Application.Audit.Journal;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("audit")]
public sealed class AuditController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("audit.read")]
    [Guards(Guard.TechnicalView)]
    public Task<IActionResult> List([FromQuery] AuditListParams query) => Ok(new GetAuditLogsQuery(query.ToFilter()));

    [HttpGet("export")]
    [RequirePermission("audit.read")]
    [Guards(Guard.TechnicalView)]
    public Task<IActionResult> Export([FromQuery] AuditListParams query) => FileOf(new ExportAuditLogsQuery(query.ToFilter()));

    [HttpGet("journal")]
    [RequirePermission("audit.read")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> Journal([FromQuery] JournalParams query) => Ok(new GetJournalQuery(query.ToFilter()));

    [HttpGet("journal/export")]
    [RequirePermission("audit.read")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> JournalExport([FromQuery] JournalParams query) => FileOf(new ExportJournalQuery(query.ToFilter()));

    [HttpGet("journal/entry/{id}")]
    [RequirePermission("audit.read")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> JournalEntry(string id) => Ok(new GetJournalEntryQuery(id));

    [HttpGet("journal/entity/{entity}/{entityId}")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> JournalForEntity(string entity, string entityId, [FromQuery] JournalParams query) => Ok(new GetJournalQuery(query.ToFilter(entity, entityId)));

    [HttpGet("entry/{id}")]
    [RequirePermission("audit.read")]
    [Guards(Guard.TechnicalView)]
    public Task<IActionResult> Detail(string id) => Ok(new GetAuditLogQuery(id));

    [HttpGet("{entity}/{entityId}")]
    [Guards(Guard.NotMerchant, Guard.TechnicalView)]
    public Task<IActionResult> ForEntity(string entity, string entityId, [FromQuery] AuditListParams query) => Ok(new GetAuditLogsQuery(query.ToFilter(entity, entityId)));
}
