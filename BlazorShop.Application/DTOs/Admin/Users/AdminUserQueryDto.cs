namespace BlazorShop.Application.DTOs.Admin.Users
{
    public class AdminUserQueryDto
    {
        public string? SearchTerm { get; set; }

        public string? Role { get; set; }

        public bool? Locked { get; set; }

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
