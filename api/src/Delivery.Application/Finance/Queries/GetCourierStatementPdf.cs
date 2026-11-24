using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

/// <summary>`GET /courier-statements/:code/pdf` : le relevé de rémunération.</summary>
public sealed record GetCourierStatementPdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetCourierStatementPdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetCourierStatementPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetCourierStatementPdfQuery request, CancellationToken cancellationToken)
    {
        var s = await CourierStatements.DetailAsync(db, caller, request.Code, cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Relevé", cancellationToken);
        var sheet = new CourierStatementSheet(company, s.Code, s.Status, s.AgencyName, s.CourierName, s.PayModel, s.SettlementMode, DateOnly.FromDateTime(s.PeriodStart),
            DateOnly.FromDateTime(s.PeriodEnd), s.LineCount, s.BaseAmount, s.CodCollected, s.CommissionsTotal, s.CommissionsDeducted, s.VariableAmount, s.TotalDue, s.ValidatedByName,
            s.Lines.Select(l => new CourierStatementSheetLine(l.WorkedAt, l.ParcelCode, l.Kind, l.CityName, l.Deducted, l.Amount)).ToList());
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{s.Code}.pdf", CacheControl: "private, no-store");
    }
}
