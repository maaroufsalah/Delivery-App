using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.PettyCash;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`PettyCashController` : la petite caisse des agences, ses journées et ses exports.</summary>
[Route("petty-cash")]
public sealed class PettyCashController(ISender sender) : ApiControllerBase(sender)
{
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>`category` : liste séparée par des virgules ; une valeur entière avec virgule (≤ 60) reste aussi candidate.</summary>
    private static IReadOnlyList<string>? Categories(string[]? raw)
    {
        if (raw is not { Length: > 0 }) return null;
        var parts = raw.SelectMany(v => v.Split(',')).Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
        if (raw.Length == 1 && raw[0].Contains(',') && raw[0].Trim().Length <= 60) parts.Add(raw[0].Trim());
        return parts.Count == 0 ? null : parts.Distinct().ToList();
    }

    private static PettyCashFilter Filter(ListQueryParams query, string? period, string? from, string? to, string? agencyId, string? kind, string[]? category, string[]? buyerId, string[]? status)
    {
        query.Order ??= "desc";
        return new PettyCashFilter(query.ToQueryWithDefault(25), period ?? "month", Clean(from), Clean(to), Clean(agencyId), Clean(kind), Categories(category), MultiValue.AsList(buyerId),
            MultiValue.AsList(status));
    }

    [HttpGet]
    [RequirePermission("pettycash.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string? period, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? agencyId,
        [FromQuery] string? kind, [FromQuery] string[]? category, [FromQuery] string[]? buyerId, [FromQuery] string[]? status) =>
        Ok(new GetPettyCashQuery(Filter(query, period, from, to, agencyId, kind, category, buyerId, status)));

    [HttpGet("meta")]
    [RequirePermission("pettycash.read")]
    public Task<IActionResult> Meta() => Ok(new GetPettyCashMetaQuery());

    [HttpPut("categories")]
    [RequirePermission("pettycash.write")]
    public Task<IActionResult> Categories([FromBody] SetPettyCashCategoriesCommand? body) => Ok(body ?? new SetPettyCashCategoriesCommand());

    [HttpGet("export")]
    [RequirePermission("pettycash.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string? period, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? agencyId,
        [FromQuery] string? kind, [FromQuery] string[]? category, [FromQuery] string[]? buyerId, [FromQuery] string[]? status) =>
        FileOf(new ExportPettyCashQuery(Filter(query, period, from, to, agencyId, kind, category, buyerId, status)));

    [HttpGet("pdf")]
    [RequirePermission("pettycash.read")]
    public Task<IActionResult> Pdf([FromQuery] ListQueryParams query, [FromQuery] string? period, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? agencyId,
        [FromQuery] string? kind, [FromQuery] string[]? category, [FromQuery] string[]? buyerId, [FromQuery] string[]? status) =>
        FileOf(new GetPettyCashPdfQuery(Filter(query, period, from, to, agencyId, kind, category, buyerId, status)));

    [HttpGet("days")]
    [RequirePermission("pettycash.read")]
    public Task<IActionResult> Days([FromQuery] string? agencyId, [FromQuery] string? status, [FromQuery] string? withDrafts, [FromQuery] string? limit) =>
        Ok(new GetPettyCashDaysQuery(Clean(agencyId), status, withDrafts, limit));

    [HttpGet("days/{day}")]
    [RequirePermission("pettycash.read")]
    public Task<IActionResult> Day(string day, [FromQuery] string? agencyId) => Ok(new GetPettyCashDayQuery(day, Clean(agencyId)));

    [HttpPost("days/{day}/close")]
    [RequirePermission("pettycash.write")]
    public Task<IActionResult> CloseDay(string day, [FromBody] PettyCashDayActionCommand? body) => Ok((body ?? new PettyCashDayActionCommand()) with { Day = day, Reopen = false });

    [HttpPost("days/{day}/reopen")]
    [RequirePermission("pettycash.write")]
    public Task<IActionResult> ReopenDay(string day, [FromBody] PettyCashDayActionCommand? body) => Ok((body ?? new PettyCashDayActionCommand()) with { Day = day, Reopen = true });

    [HttpGet("days/{day}/export")]
    [RequirePermission("pettycash.read")]
    public Task<IActionResult> ExportDay(string day, [FromQuery] string? agencyId) => FileOf(new ExportPettyCashDayQuery(day, Clean(agencyId)));

    [HttpGet("days/{day}/pdf")]
    [RequirePermission("pettycash.read")]
    public Task<IActionResult> PdfDay(string day, [FromQuery] string? agencyId) => FileOf(new GetPettyCashDayPdfQuery(day, Clean(agencyId)));

    [HttpPost]
    [RequirePermission("pettycash.write")]
    public Task<IActionResult> Create([FromBody] PettyCashMovementCommand? body) => Created((body ?? new PettyCashMovementCommand()) with { Id = null });

    [HttpPatch("{id}")]
    [RequirePermission("pettycash.write")]
    public Task<IActionResult> Update(string id, [FromBody] PettyCashMovementCommand? body) => Ok((body ?? new PettyCashMovementCommand()) with { Id = id });

    [HttpDelete("{id}")]
    [RequirePermission("pettycash.write")]
    public async Task<IActionResult> Remove(string id)
    {
        await Sender.Send(new DeletePettyCashCommand(id), HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPatch("{id}/validate")]
    [RequirePermission("pettycash.write")]
    public Task<IActionResult> Validate(string id) => Ok(new DecidePettyCashCommand { Id = id, Validate = true });

    [HttpPatch("{id}/refuse")]
    [RequirePermission("pettycash.write")]
    public Task<IActionResult> Refuse(string id, [FromBody] DecidePettyCashCommand? body) => Ok((body ?? new DecidePettyCashCommand()) with { Id = id, Validate = false });

    [HttpGet("{id}/proof")]
    [RequirePermission("pettycash.read")]
    public Task<IActionResult> Proof(string id) => FileOf(new GetPettyCashProofQuery(id));
}

/// <summary>`PettyCashCourierController` : les dépenses du livreur depuis son téléphone.</summary>
[Route("courier/me/petty-cash")]
[Guards(Guard.Courier)]
public sealed class PettyCashCourierController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("pettycash.courier")]
    public Task<IActionResult> List([FromQuery] string? page, [FromQuery] string? pageSize) => Ok(new GetMyPettyCashQuery(page, pageSize));

    [HttpGet("export")]
    [RequirePermission("pettycash.courier")]
    public Task<IActionResult> Export() => FileOf(new ExportMyPettyCashQuery());

    [HttpPost]
    [RequirePermission("pettycash.courier")]
    public Task<IActionResult> Create([FromBody] MyPettyCashExpenseCommand? body, [FromHeader(Name = "idempotency-key")] string? idempotencyKey) =>
        Created((body ?? new MyPettyCashExpenseCommand()) with { Id = null, IdempotencyKey = idempotencyKey });

    [HttpPatch("{id}")]
    [RequirePermission("pettycash.courier")]
    public Task<IActionResult> Update(string id, [FromBody] MyPettyCashExpenseCommand? body) => Ok((body ?? new MyPettyCashExpenseCommand()) with { Id = id });

    [HttpDelete("{id}")]
    [RequirePermission("pettycash.courier")]
    public async Task<IActionResult> Remove(string id)
    {
        await Sender.Send(new DeleteMyPettyCashCommand(id), HttpContext.RequestAborted);
        return NoContent();
    }
}
