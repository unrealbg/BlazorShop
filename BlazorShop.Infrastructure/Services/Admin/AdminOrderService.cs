namespace BlazorShop.Infrastructure.Services.Admin
{
    using System.Text.Json;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Admin.Orders;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public class AdminOrderService : IAdminOrderService
    {
        private readonly AppDbContext _db;
        private readonly IOrderTrackingService _trackingService;
        private readonly IAdminAuditService _auditService;

        public AdminOrderService(AppDbContext db, IOrderTrackingService trackingService, IAdminAuditService auditService)
        {
            _db = db;
            _trackingService = trackingService;
            _auditService = auditService;
        }

        public async Task<PagedResult<GetOrder>> GetAsync(AdminOrderQueryDto query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var pageNumber = Math.Max(1, query.PageNumber);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var orders = _db.Orders.Include(order => order.Lines).AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                var search = query.SearchTerm.Trim().ToLowerInvariant();
                var matchingUserIds = await _db.Users
                    .AsNoTracking()
                    .Where(user =>
                        (user.Email != null && user.Email.ToLower().Contains(search)) ||
                        (user.UserName != null && user.UserName.ToLower().Contains(search)) ||
                        user.FullName.ToLower().Contains(search))
                    .Select(user => user.Id)
                    .ToArrayAsync();

                orders = orders.Where(order =>
                    order.Reference.ToLower().Contains(search) ||
                    matchingUserIds.Contains(order.UserId));
            }

            if (query.OrderStatus.HasValue)
            {
                orders = orders.Where(order => order.OrderStatus == query.OrderStatus.Value);
            }

            if (query.PaymentStatus.HasValue)
            {
                orders = orders.Where(order => order.PaymentStatus == query.PaymentStatus.Value);
            }

            if (query.FulfillmentStatus.HasValue)
            {
                orders = orders.Where(order => order.FulfillmentStatus == query.FulfillmentStatus.Value);
            }

            if (query.FromUtc.HasValue)
            {
                orders = orders.Where(order => order.CreatedOn >= EnsureUtc(query.FromUtc.Value));
            }

            if (query.ToUtc.HasValue)
            {
                orders = orders.Where(order => order.CreatedOn <= EnsureUtc(query.ToUtc.Value));
            }

            var total = await orders.CountAsync();
            var page = await orders
                .OrderByDescending(order => order.CreatedOn)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<GetOrder>
            {
                Items = await MapOrdersAsync(page),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = total,
            };
        }

        public async Task<ServiceResponse<GetOrder>> GetByIdAsync(Guid id)
        {
            var order = await GetOrderEntityAsync(id);
            return order is null
                ? Failure("Order not found.", ServiceResponseType.NotFound)
                : Success((await MapOrdersAsync(new[] { order })).Single(), "Order retrieved successfully.");
        }

        public async Task<ServiceResponse<GetOrder>> UpdateTrackingAsync(Guid id, UpdateTrackingRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (id == Guid.Empty)
            {
                return Failure("Order id is required.", ServiceResponseType.ValidationError);
            }

            var updated = await _trackingService.UpdateTrackingDetailsAsync(
                id,
                request.Carrier?.Trim() ?? string.Empty,
                request.TrackingNumber?.Trim() ?? string.Empty,
                request.TrackingUrl?.Trim() ?? string.Empty);

            if (!updated.Success)
            {
                return Failure(updated.ErrorMessage ?? "Tracking could not be updated.", ToResponseType(updated.Outcome));
            }

            var order = await GetOrderEntityAsync(id);
            if (updated.Outcome == OrderTrackingTransitionOutcome.Applied)
            {
                await LogAsync("Order.TrackingUpdated", id, "Order tracking updated.", request);
            }
            return Success((await MapOrdersAsync(new[] { order! })).Single(), "Order tracking updated successfully.");
        }

        public async Task<ServiceResponse<GetOrder>> UpdateShippingStatusAsync(Guid id, UpdateShippingStatusRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (id == Guid.Empty)
            {
                return Failure("Order id is required.", ServiceResponseType.ValidationError);
            }

            if (string.IsNullOrWhiteSpace(request.FulfillmentStatus)
                || !Enum.TryParse<FulfillmentStatus>(request.FulfillmentStatus, true, out var fulfillmentStatus)
                || !Enum.IsDefined(fulfillmentStatus))
            {
                return Failure("Fulfillment status is invalid.", ServiceResponseType.ValidationError);
            }

            var updated = await _trackingService.TransitionFulfillmentAsync(
                id,
                fulfillmentStatus.ToString(),
                request.ShippedOn,
                request.DeliveredOn);

            if (!updated.Success)
            {
                return Failure(updated.ErrorMessage ?? "Fulfillment could not be updated.", ToResponseType(updated.Outcome));
            }

            var order = await GetOrderEntityAsync(id);
            if (updated.Outcome == OrderTrackingTransitionOutcome.Applied)
            {
                await LogAsync("Order.FulfillmentStatusUpdated", id, "Order fulfillment status updated.", request);
            }
            return Success((await MapOrdersAsync(new[] { order! })).Single(), "Order shipping status updated successfully.");
        }

        public async Task<ServiceResponse<GetOrder>> UpdateAdminNoteAsync(Guid id, UpdateOrderAdminNoteRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (id == Guid.Empty)
            {
                return Failure("Order id is required.", ServiceResponseType.ValidationError);
            }

            if (request.AdminNote?.Length > 2000)
            {
                return Failure("Admin note must be 2,000 characters or fewer.", ServiceResponseType.ValidationError);
            }

            var order = await _db.Orders.Include(item => item.Lines).FirstOrDefaultAsync(item => item.Id == id);
            if (order is null)
            {
                return Failure("Order not found.", ServiceResponseType.NotFound);
            }

            order.AdminNote = string.IsNullOrWhiteSpace(request.AdminNote) ? null : request.AdminNote.Trim();
            await _db.SaveChangesAsync();
            await LogAsync("Order.AdminNoteUpdated", id, "Order admin note updated.", new { HasNote = !string.IsNullOrWhiteSpace(order.AdminNote) });

            return Success((await MapOrdersAsync(new[] { order })).Single(), "Order admin note updated successfully.");
        }

        private async Task<Order?> GetOrderEntityAsync(Guid id)
        {
            return id == Guid.Empty
                ? null
                : await _db.Orders.Include(order => order.Lines).AsNoTracking().FirstOrDefaultAsync(order => order.Id == id);
        }

        private Task<IReadOnlyList<GetOrder>> MapOrdersAsync(IReadOnlyCollection<Order> orders)
        {
            IReadOnlyList<GetOrder> result = orders.Select(order =>
            {
                return new GetOrder
                {
                    Id = order.Id,
                    Reference = order.Reference,
                    OrderStatus = order.OrderStatus.ToString(),
                    PaymentStatus = order.PaymentStatus.ToString(),
                    PaymentMethod = order.PaymentMethod.ToString(),
                    FulfillmentStatus = order.FulfillmentStatus.ToString(),
                    TotalAmount = order.TotalAmount,
                    SubtotalAmount = order.SubtotalAmount,
                    DiscountAmount = order.DiscountAmount,
                    ShippingAmount = order.ShippingAmount,
                    TaxAmount = order.TaxAmount,
                    Currency = order.Currency,
                    CreatedOn = order.CreatedOn,
                    ShippingCarrier = order.ShippingCarrier,
                    TrackingNumber = order.TrackingNumber,
                    TrackingUrl = order.TrackingUrl,
                    ShippedOn = order.ShippedOn,
                    DeliveredOn = order.DeliveredOn,
                    UserId = order.UserId,
                    CustomerName = order.CustomerNameSnapshot,
                    CustomerEmail = order.CustomerEmailSnapshot,
                    ShippingAddress = order.ShippingAddressSnapshot,
                    BillingAddress = order.BillingAddressSnapshot,
                    AdminNote = order.AdminNote,
                    Version = order.Version,
                    Lines = order.Lines.Select(line =>
                    {
                        return new GetOrderLine
                        {
                            ProductId = line.ProductId,
                            VariantId = line.ProductVariantId,
                            Quantity = line.Quantity,
                            UnitPrice = line.UnitPrice,
                            LineTotal = line.LineTotal,
                            ProductName = line.ProductNameSnapshot,
                            Sku = line.SkuSnapshot,
                            SizeScale = line.SizeScaleSnapshot,
                            SizeValue = line.SizeValueSnapshot,
                            Color = line.ColorSnapshot,
                        };
                    }),
                };
            }).ToArray();
            return Task.FromResult(result);
        }

        private async Task LogAsync(string action, Guid orderId, string summary, object metadata)
        {
            await _auditService.LogAsync(new CreateAdminAuditLogDto
            {
                Action = action,
                EntityType = "Order",
                EntityId = orderId.ToString(),
                Summary = summary,
                MetadataJson = JsonSerializer.Serialize(metadata),
            });
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            };
        }

        private static ServiceResponse<GetOrder> Success(GetOrder payload, string message)
        {
            return new ServiceResponse<GetOrder>(true, message, payload.Id)
            {
                Payload = payload,
                ResponseType = ServiceResponseType.Success,
            };
        }

        private static ServiceResponse<GetOrder> Failure(string message, ServiceResponseType responseType)
        {
            return new ServiceResponse<GetOrder>(false, message)
            {
                ResponseType = responseType,
            };
        }

        private static ServiceResponseType ToResponseType(OrderTrackingTransitionOutcome outcome)
        {
            return outcome switch
            {
                OrderTrackingTransitionOutcome.NotFound => ServiceResponseType.NotFound,
                OrderTrackingTransitionOutcome.ValidationError => ServiceResponseType.ValidationError,
                _ => ServiceResponseType.Conflict,
            };
        }
    }
}
