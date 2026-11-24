using System.Text.Json;
using System.Threading.Channels;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.BulkJobs;

public static class BulkJobKinds
{
    public const string RouteOutcomes = "route-outcomes";
    public const string NoteValidate = "note-validate";
    public const string ParcelTransfer = "parcel-transfer";
    public const string RouteDispatch = "route-dispatch";
    public const int ChunkSize = 250;
}
