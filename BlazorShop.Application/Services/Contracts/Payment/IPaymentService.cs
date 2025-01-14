﻿namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Domain.Entities;

    public interface IPaymentService
    {
        Task<ServiceResponse> Pay(
            decimal totalAmount,
            IEnumerable<Product> cartProducts,
            IEnumerable<ProcessCart> carts);
    }
}
