using System.Text.Json;
using Border.Application.Auditing;
using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Border.Infrastructure.Auditing;

internal sealed class AuditWriter(BorderDbContext dbContext, IHttpContextAccessor httpContextAccessor) : IAuditWriter
{
    public async Task WriteAsync(string action, string entityType, string entityId, object? oldValues, object? newValues, CancellationToken cancellationToken = default)
    {
        var context = httpContextAccessor.HttpContext;
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = context?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = context?.Connection.RemoteIpAddress?.ToString()
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
