namespace BlazorShop.Application.Validations.Authentication
{
    using BlazorShop.Application.DTOs.UserIdentity;
    using FluentValidation;

    public class ChangePasswordValidator : AbstractValidator<ChangePassword>
    {
        public ChangePasswordValidator()
        {
            RuleFor(x => x.CurrentPassword).NotEmpty();
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6).WithMessage("Password must be at least 6 characters.");
            RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithMessage("Passwords must match.");
        }
    }
}
