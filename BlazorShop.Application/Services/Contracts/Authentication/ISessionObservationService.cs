namespace BlazorShop.Application.Services.Contracts.Authentication
{
    using BlazorShop.Application.DTOs;

    public interface ISessionObservationService
    {
        Task<SessionObservation> ObserveAsync(string refreshToken);
    }
}
