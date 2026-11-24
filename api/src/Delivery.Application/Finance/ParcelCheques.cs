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
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>Ce que le chèque d'un colis retranche de ce que la société verse (`chequePaidOn`) : lu par le versement et par la facture.</summary>
public static class ParcelCheques
{
    public sealed record Remitted(decimal Cheque, string? ConfirmedParcelId);

    public static long PaidOn(decimal? chequeAmount, ChequeStatus? status, IReadOnlyList<Remitted> remitted)
    {
        var line = remitted.FirstOrDefault(r => r.ConfirmedParcelId is not null) ?? remitted.FirstOrDefault();
        return CourierPay.ChequePaidOn(chequeAmount is null ? null : Money.ToCents(chequeAmount.Value), status == ChequeStatus.ANNULE, line is null ? null : Money.ToCents(line.Cheque));
    }

    public static long Payable(decimal codAmount, decimal deliveryFee, bool feeIncluded, bool alreadyPaid, decimal? chequeAmount, ChequeStatus? status, IReadOnlyList<Remitted> remitted) =>
        Billing.CodPayoutAmount(Billing.CodToCollect(Money.ToCents(codAmount), Money.ToCents(deliveryFee), feeIncluded, alreadyPaid), PaidOn(chequeAmount, status, remitted));
}
