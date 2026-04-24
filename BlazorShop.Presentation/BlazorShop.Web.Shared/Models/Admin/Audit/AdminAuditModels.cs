namespace BlazorShop.Web.Shared.Models.Admin.Audit
{
    public class AdminAuditLog
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

    public class AdminAuditQuery
    {
        public string? Actor { get; set; }

        public string? Action { get; set; }

        public string? EntityType { get; set; }

        public string? EntityId { get; set; }

        public DateTime? FromUtc { get; set; }

        public DateTime? ToUtc { get; set; }

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 25;
    }
}
