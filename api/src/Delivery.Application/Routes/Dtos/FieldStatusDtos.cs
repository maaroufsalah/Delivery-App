using System.Text.Json;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Commands;
using Delivery.Application.Parcels.Labels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

public sealed record FieldStatusChangeDto(string Code, int From, int To);

public sealed record FieldStatusRefusalDto(string Code, string Reason, string Message);

public sealed record AdminFieldStatusResultDto(IReadOnlyList<FieldStatusChangeDto> Changed, IReadOnlyList<FieldStatusRefusalDto> Refused);
