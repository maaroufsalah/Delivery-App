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

/// <summary>`GET /petty-cash/pdf`.</summary>
public sealed record GetPettyCashPdfQuery(PettyCashFilter Filter) : IRequest<FileContent>;

internal sealed class GetPettyCashPdfHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IPdfService pdf, IFileStorage storage) : IRequestHandler<GetPettyCashPdfQuery, FileContent>
{
    public Task<FileContent> Handle(GetPettyCashPdfQuery request, CancellationToken cancellationToken) =>
        PettyCashExports.PdfAsync(db, caller, clock, pdf, storage, request.Filter, null, cancellationToken);
}
