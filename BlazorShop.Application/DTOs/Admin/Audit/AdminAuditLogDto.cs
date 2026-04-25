namespace BlazorShop.Application.DTOs.Admin.Audit
{
    public class AdminAuditLogDto
    {
        public Guid Id { get; set; }

        public string? ActorUserId { get; set; }

        public string? ActorEmail { get; set; }

        public string Action { get; set; } = string.Empty;

        public string EntityType { get; set; } = string.Empty;

        public string? EntityId { get; set; }

        public string Summary { get; set; } = string.Empty;

        public string? MetadataJson { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTime CreatedOn { get; set; }
    }
}
