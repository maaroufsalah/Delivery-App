using System.Text.Json;
using System.Threading.Channels;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.BulkJobs;

/// <summary>L'identité du demandeur, rejouée par la tâche (`scope` de Speedy).</summary>
public sealed record CallerSnapshot(string UserId, string? CompanyId, string Email, IReadOnlyList<RoleName> Roles, string? AgencyId, string? ClientId, string? CourierId);
