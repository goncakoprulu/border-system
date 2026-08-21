namespace Border.Application.Auditing;

public interface IAuditWriter
{
    Task WriteAsync(string action, string entityType, string entityId, object? oldValues, object? newValues, CancellationToken cancellationToken = default);
}
