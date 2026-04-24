namespace BlazorShop.Domain.Entities
{
    public class AdminAuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string? ActorUserId { get; set; }

        public string? ActorEmail { get; set; }

        public string Action { get; set; } = string.Empty;

        public string EntityType { get; set; } = string.Empty;

        public string? EntityId { get; set; }

        public string Summary { get; set; } = string.Empty;

        public string? MetadataJson { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }
}
