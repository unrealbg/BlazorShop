﻿namespace BlazorShop.Domain.Entities.Payment
{
    using System.ComponentModel.DataAnnotations;

    public class OrderItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ProductId { get; set; }

        public int Quantity { get; set; }

        public string? UserId { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }
}
