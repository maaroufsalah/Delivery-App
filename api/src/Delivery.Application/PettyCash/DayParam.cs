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

internal static class DayParam
{
    public static void Check(string day)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(day, @"^\d{4}-\d{2}-\d{2}$")) throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "", message = "invalid-date" } } });
    }
}
