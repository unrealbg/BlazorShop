namespace BlazorShop.Application.DTOs.UserIdentity
{
    public class CreateUser : BaseModel
    {
        public required string FullName { get; set; }

        public required string ConfirmPassword { get; set; }
    }
}
