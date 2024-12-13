namespace BlazorShop.Application.Validations
{
    using BlazorShop.Application.DTOs;

    using FluentValidation;

    public interface IValidationService
    {
        Task<ServiceResponse> ValidateAsync<T>(T model, IValidator<T> validator);
    }
}
