namespace BlazorShop.Application.DTOs.Admin.Audit
{
    public class AdminAuditQueryDto
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
