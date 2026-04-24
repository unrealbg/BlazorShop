namespace BlazorShop.Application.Services.Contracts.Admin
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Orders;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Domain.Contracts;

    public interface IAdminOrderService
    {
        Task<PagedResult<GetOrder>> GetAsync(AdminOrderQueryDto query);

        Task<ServiceResponse<GetOrder>> GetByIdAsync(Guid id);

        Task<ServiceResponse<GetOrder>> UpdateTrackingAsync(Guid id, UpdateTrackingRequest request);

        Task<ServiceResponse<GetOrder>> UpdateShippingStatusAsync(Guid id, UpdateShippingStatusRequest request);

        Task<ServiceResponse<GetOrder>> UpdateAdminNoteAsync(Guid id, UpdateOrderAdminNoteRequest request);
    }
}
