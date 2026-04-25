namespace BlazorShop.Application.DTOs.Admin.Audit
{
    public class CreateAdminAuditLogDto
    {
        public string Action { get; set; } = string.Empty;

        public string EntityType { get; set; } = string.Empty;

        public string? EntityId { get; set; }

        public string Summary { get; set; } = string.Empty;

        public string? MetadataJson { get; set; }
    }
}
