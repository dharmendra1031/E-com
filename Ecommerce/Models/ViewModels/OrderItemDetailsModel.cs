namespace EcommerceWebApplication.Models.ViewModels
{

    public class OrderItemDetailsModel
    {
        // From OrderItems
        public int OrderItemId { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }

        // From Products
        public string NameArb { get; set; }
        public string Name { get; set; }
        public int SubCategoryId { get; set; }
        public int CategoryId { get; set; }

        // From Orders
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal VatAmount { get; set; }
        public int CustomerId { get; set; }

        // From Users
        public string FullName { get; set; }
        public string FullNameArb { get; set; }

        // ✅ FIXED TYPES
        public int PaymentStatus { get; set; }   // was string ❌
        public string PaymentType { get; set; }  // stays string
        public int Status { get; set; }          // was string ❌

        // From OrderStatus
        public string OrderStatusNameArb { get; set; }
        public string OrderstatusName { get; set; }
        public byte[]? Logo { get; set; }

        // From Categories

        public string CName { get; set; }

        public string CNameArb { get; set; }

    }


}

