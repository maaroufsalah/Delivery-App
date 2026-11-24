using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

/// <summary>`GET /petty-cash/days/:day/pdf` : la journée, avec son bandeau d'ouverture et de clôture.</summary>
public sealed record GetPettyCashDayPdfQuery(string Day, string? AgencyId) : IRequest<FileContent>;

internal sealed class GetPettyCashDayPdfHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IPdfService pdf, IFileStorage storage) : IRequestHandler<GetPettyCashDayPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetPettyCashDayPdfQuery request, CancellationToken cancellationToken)
    {
        DayParam.Check(request.Day);
        var detail = await PettyCashDays.DetailAsync(db, caller, request.Day, request.AgencyId, cancellationToken);
        return await PettyCashExports.PdfAsync(db, caller, clock, pdf, storage, PettyCashExports.Day(request.Day, request.AgencyId), detail.Summary, cancellationToken);
    }
}
