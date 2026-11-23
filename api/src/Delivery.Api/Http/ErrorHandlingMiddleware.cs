using System.Text.Json;
using Delivery.Domain.Common;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Delivery.Api.Http;

/// <summary>
/// Équivalent du `AllExceptionsFilter` de Speedy : toute erreur sort en
/// `{ code, message, details? }` avec le statut du code.
/// </summary>
public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json)
{
    public const string BodyLimitLabel = "2mb";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
            await WriteNotFoundIfEmptyAsync(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var (status, body) = Describe(exception);
            if (status >= 500)
            {
                logger.LogError(exception, "erreur serveur {Method} {Path}", context.Request.Method, context.Request.Path);
            }
            else
            {
                logger.LogWarning("{Status} {Code} {Method} {Path} : {Message}", status, body.Code, context.Request.Method, context.Request.Path, body.Message);
            }
            await WriteAsync(context, status, body);
        }
    }

    /// <summary>Route inconnue : Express répond `Cannot GET /api/x` en 404, verbe compris.</summary>
    private async Task WriteNotFoundIfEmptyAsync(HttpContext context)
    {
        if (context.Response.HasStarted) return;
        var status = context.Response.StatusCode;
        if (status is not (404 or 405)) return;
        if (context.GetEndpoint() is not null && status == 404) return;
        context.Response.Headers.Remove("Allow");
        await WriteAsync(context, 404, new ErrorBody(ErrorCode.NOT_FOUND.ToString(), $"Cannot {context.Request.Method} {context.Request.Path}{context.Request.QueryString}"));
    }

    internal static (int Status, ErrorBody Body) Describe(Exception exception)
    {
        switch (exception)
        {
            case BusinessException business:
                return (StatusOf(business.Code), new ErrorBody(business.Code.ToString(), business.Message, business.Details));
            case DbUpdateException { InnerException: PostgresException pg } when pg.SqlState == PostgresErrorCodes.UniqueViolation:
                return (409, new ErrorBody("CONFLICT", "Cette valeur existe déjà.", new { fields = UniqueFields(pg) }));
            case DbUpdateException { InnerException: PostgresException pg } when pg.SqlState == PostgresErrorCodes.ForeignKeyViolation:
                return (422, new ErrorBody("VALIDATION_FAILED", "Référence inconnue.", new { field = pg.ConstraintName }));
            case DbUpdateConcurrencyException:
                return (404, new ErrorBody("NOT_FOUND", "Ressource introuvable."));
            case BadHttpRequestException { StatusCode: 413 }:
                return (413, new ErrorBody("PAYLOAD_TOO_LARGE", "Le contenu envoyé est trop volumineux.", new { reason = "payload-too-large", limit = BodyLimitLabel }));
            case BadHttpRequestException bad:
                return (bad.StatusCode, new ErrorBody(CodeFromStatus(bad.StatusCode), bad.Message));
            default:
                return (500, new ErrorBody("INTERNAL", "Erreur interne."));
        }
    }

    private static string[]? UniqueFields(PostgresException pg)
    {
        // Prisma rend `meta.target` : la liste des colonnes de l'index unique.
        var detail = pg.Detail;
        if (detail is null) return null;
        var start = detail.IndexOf('(', StringComparison.Ordinal);
        var end = detail.IndexOf(')', StringComparison.Ordinal);
        if (start < 0 || end <= start) return null;
        return detail[(start + 1)..end].Split(',').Select(s => s.Trim().Trim('"')).ToArray();
    }

    public static int StatusOf(ErrorCode code) => code switch
    {
        ErrorCode.UNAUTHENTICATED => 401,
        ErrorCode.FORBIDDEN => 403,
        ErrorCode.NOT_FOUND => 404,
        ErrorCode.CONFLICT => 409,
        ErrorCode.VALIDATION_FAILED => 422,
        ErrorCode.TRANSITION_FORBIDDEN => 422,
        ErrorCode.PAYLOAD_TOO_LARGE => 413,
        ErrorCode.TOO_MANY_REQUESTS => 429,
        _ => 500,
    };

    public static string CodeFromStatus(int status) => status switch
    {
        401 => "UNAUTHENTICATED",
        403 => "FORBIDDEN",
        413 => "PAYLOAD_TOO_LARGE",
        404 => "NOT_FOUND",
        409 => "CONFLICT",
        422 or 400 => "VALIDATION_FAILED",
        _ => "INTERNAL",
    };

    private async Task WriteAsync(HttpContext context, int status, ErrorBody body)
    {
        context.Response.Clear();
        if (status == 429 && body.Details is not null)
        {
            var retry = JsonSerializer.SerializeToElement(body.Details, json.Value.SerializerOptions);
            if (retry.ValueKind == JsonValueKind.Object && retry.TryGetProperty("retryAfterSeconds", out var seconds) && seconds.ValueKind == JsonValueKind.Number)
            {
                context.Response.Headers.RetryAfter = seconds.GetRawText();
            }
        }
        context.Response.StatusCode = status;
        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await context.Response.WriteAsJsonAsync(body, json.Value.SerializerOptions);
    }
}

/// <summary>`ApiErrorBody` : `details` absent quand il n'y en a pas.</summary>
public sealed record ErrorBody(string Code, string Message, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] object? Details = null);
