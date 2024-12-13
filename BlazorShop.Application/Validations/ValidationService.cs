namespace BlazorShop.Application.Validations
{
    using BlazorShop.Application.DTOs;

    using FluentValidation;

    public class ValidationService : IValidationService
    {
        public async Task<ServiceResponse> ValidateAsync<T>(T model, IValidator<T> validator)
        {
            var validationResult = await validator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                string errorMessage = string.Join("; ", errors);
                return new ServiceResponse { Message = errorMessage };
            }

            return new ServiceResponse { Success = true };
        }
    }
}
