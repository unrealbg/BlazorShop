namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Admin.Orders;
    using BlazorShop.Web.Shared.Models.Payment;

    public interface IAdminOrderService
    {
        Task<QueryResult<PagedResult<GetOrder>>> GetAsync(AdminOrderQuery query);

        Task<QueryResult<GetOrder>> GetByIdAsync(Guid id);

        Task<ServiceResponse<GetOrder>> UpdateTrackingAsync(Guid id, UpdateTrackingRequest request);

        Task<ServiceResponse<GetOrder>> UpdateShippingStatusAsync(Guid id, UpdateShippingStatusRequest request);

        Task<ServiceResponse<GetOrder>> UpdateAdminNoteAsync(Guid id, UpdateOrderAdminNote request);
    }
}
