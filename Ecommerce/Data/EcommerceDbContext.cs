using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data;
using System.Reflection.Emit;
using static EcommerceWebApplication.Controllers.EcommerceApiController;
using static EcommerceWebApplication.Controllers.ReportController;

namespace EcommerceWebApplication.Data
{
    public class EcommerceDbContext:DbContext
    {
        public EcommerceDbContext(DbContextOptions<EcommerceDbContext> options)
            : base(options)
        {
        }
        public DbSet<PageMaster> PageMaster { get; set; }

        // ✅ ROLES & USERS
        public DbSet<Role> Roles { get; set; }
        public DbSet<RolePermission> RolePermission { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<VendorUser> VendorUsers { get; set; }
        public DbSet<VendorListing> VendorListings { get; set; }
        public DbSet<Customers> Customers { get; set; }

        //public DbSet<Driver> Drivers { get; set; }

        // ✅ CATEGORY STRUCTURE
        public DbSet<Category> Categories { get; set; }
        public DbSet<SubCategory> SubCategories { get; set; }

        // ✅ PRODUCTS
        public DbSet<Products> Products { get; set; }
        public DbSet<WekendDealsModel> WekendDeals { get; set; }
        public DbSet<BannerModel> Banner { get; set; }
        public DbSet<ProductImages> ProductImages { get; set; }
        public DbSet<ProductReview> ProductReviews { get; set; }
        public DbSet<BrandModel> Brand { get; set; }


        //✅ ORDER FLOW
        public DbSet<OrderModel> Orders { get; set; }
        public DbSet<OrderItemmodel> OrderItems { get; set; }
        public DbSet<OrderStatus> OrderStatus { get; set; }
        public DbSet<Payments> Payments { get; set; }
        public DbSet<Paymentstatus> PaymentStatus { get; set; }
        public DbSet<ProductStock> ProductStocks { get; set; }
        public DbSet<ReturnRequest> ReturnRequests { get; set; }
        public DbSet<UserAddress> UserAddresses { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<CouponUsage> CouponUsage { get; set; }
        public DbSet<ChangeOrderStatus> ChangeOrderStatus { get; set; }
        public DbSet<CompanyDetails> CompanyDetails { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<TopCategory> TopCategories { get; set; }

        // About Flow
        public DbSet<AboutUs> AboutUs { get; set; }

        // Blog Flow
        public DbSet<blogMaster> blogmaster { get; set; }
        public DbSet<OrderItemDetailsModel> OrderItemDetailsModels { get; set; }
        public DbSet<UserOtp> UserOtps { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<SendNotificationModel> SendNotification { get; set; }
        public DbSet<Area> Area { get; set; }
        public DbSet<Block> Block { get; set; }
        public DbSet<ColorsModel> colors { get; set; }
        public DbSet<SpecificationModel> Specification { get; set; }
        //public DbSet<GuestForm> GuestForms { get; set; }
        //Reports start
        public DbSet<CustomerReportRow> CustomerReportRows { get; set; }
        public DbSet<ProductStockViewModel> ProductStockViewModel { get; set; }
        public DbSet<ItemFileReportRow> ItemFileReportRow { get; set; }
        public DbSet<CategoryReportModel> CategoryReportModel { get; set; }
        public DbSet<DayWiseSaleReportModel> DayWiseSaleReportModel { get; set; }
        public DbSet<OrderProductViewModel> OrderProductViewModel { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Legacy cart paths must not read or mutate marketplace lines.
            modelBuilder.Entity<CartItem>().HasQueryFilter(c => c.VendorListingId == null);
            // Prevent existing EF-based order/stock paths from consuming staged seller orders.
            modelBuilder.Entity<OrderModel>().HasQueryFilter(o => o.MarketplaceRequestId == null);
            modelBuilder.Entity<OrderItemmodel>().HasQueryFilter(o => o.VendorOrderId == null);
            modelBuilder.Entity<VendorListing>(entity =>
            {
                entity.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(x => new { x.VendorId, x.SellerSku }).IsUnique();
                entity.HasIndex(x => new { x.VendorId, x.ProductId }).IsUnique();
            });
            modelBuilder.Entity<VendorUser>(entity =>
            {
                entity.HasKey(x => new { x.VendorId, x.UserId });
                entity.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<OrderItemDetailsModel>().HasNoKey();
            modelBuilder.Entity<CustomerReportRow>().HasNoKey().ToView(null); // no table mapping
            modelBuilder.Entity<ItemFileReportRow>().HasNoKey().ToView(null); // no table mapping
            modelBuilder.Entity<CategoryReportModel>().HasNoKey().ToView(null); // no table mapping
            modelBuilder.Entity<DayWiseSaleReportModel>().HasNoKey().ToView(null); // no table mapping
            modelBuilder.Entity<OrderProductViewModel>().HasNoKey().ToView(null); // no table mapping
            base.OnModelCreating(modelBuilder);
        }
        // ✅ COUPONS & COMPLAINTS
        //public DbSet<Coupon> Coupons { get; set; }
        //public DbSet<Complaint> Complaints { get; set; }

        // ✅ WALLET SYSTEM
        //public DbSet<Wallet> Wallets { get; set; }
        //public DbSet<WalletTransaction> WalletTransactions { get; set; }

        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    base.OnModelCreating(modelBuilder);

        //    // ✅ TABLE NAMES EXACTLY AS YOUR SQL
        //    modelBuilder.Entity<Role>().ToTable("Role");
        //    modelBuilder.Entity<User>().ToTable("Users");
        //    modelBuilder.Entity<Driver>().ToTable("Drivers");
        //    modelBuilder.Entity<Category>().ToTable("Categories");
        //    modelBuilder.Entity<SubCategory>().ToTable("SubCategories");
        //    modelBuilder.Entity<Product>().ToTable("Products");
        //    modelBuilder.Entity<ProductImage>().ToTable("ProductImages");
        //    modelBuilder.Entity<ProductReview>().ToTable("ProductReviews");
        //    modelBuilder.Entity<Order>().ToTable("Orders");
        //    modelBuilder.Entity<OrderItem>().ToTable("OrderItems");
        //    modelBuilder.Entity<Payment>().ToTable("Payments");
        //    modelBuilder.Entity<Coupon>().ToTable("Coupons");
        //    modelBuilder.Entity<Complaint>().ToTable("Complaints");
        //    modelBuilder.Entity<Wallet>().ToTable("Wallets");
        //    modelBuilder.Entity<WalletTransaction>().ToTable("WalletTransactions");

        //    // ✅ RELATIONSHIP: Role → Users
        //    modelBuilder.Entity<User>()
        //        .HasOne<Role>()
        //        .WithMany()
        //        .HasForeignKey(u => u.RoleId)
        //        .OnDelete(DeleteBehavior.Restrict);

        //    // ✅ RELATIONSHIP: Product → ProductImages
        //    modelBuilder.Entity<ProductImage>()
        //        .HasOne<Product>()
        //        .WithMany()
        //        .HasForeignKey(pi => pi.ProductId)
        //        .OnDelete(DeleteBehavior.Cascade);

        //    // ✅ RELATIONSHIP: Category → SubCategory
        //    modelBuilder.Entity<SubCategory>()
        //        .HasOne<Category>()
        //        .WithMany()
        //        .HasForeignKey(sc => sc.ParentCategoryId)
        //        .OnDelete(DeleteBehavior.Cascade);

        //    // ✅ RELATIONSHIP: Product → Category + SubCategory
        //    modelBuilder.Entity<Product>()
        //        .HasOne<Category>()
        //        .WithMany()
        //        .HasForeignKey(p => p.CategoryId)
        //        .OnDelete(DeleteBehavior.Restrict);

        //    modelBuilder.Entity<Product>()
        //        .HasOne<SubCategory>()
        //        .WithMany()
        //        .HasForeignKey(p => p.SubCategoryId)
        //        .OnDelete(DeleteBehavior.Restrict);

        //    // ✅ RELATIONSHIP: Order → User
        //    modelBuilder.Entity<Order>()
        //        .HasOne<User>()
        //        .WithMany()
        //        .HasForeignKey(o => o.UserId)
        //        .OnDelete(DeleteBehavior.Restrict);

        //    // ✅ RELATIONSHIP: OrderItem → Order + Product
        //    modelBuilder.Entity<OrderItem>()
        //        .HasOne<Order>()
        //        .WithMany()
        //        .HasForeignKey(oi => oi.OrderId)
        //        .OnDelete(DeleteBehavior.Cascade);

        //    modelBuilder.Entity<OrderItem>()
        //        .HasOne<Product>()
        //        .WithMany()
        //        .HasForeignKey(oi => oi.ProductId)
        //        .OnDelete(DeleteBehavior.Restrict);

        //    // ✅ RELATIONSHIP: Payment → Order
        //    modelBuilder.Entity<Payment>()
        //        .HasOne<Order>()
        //        .WithMany()
        //        .HasForeignKey(p => p.OrderId)
        //        .OnDelete(DeleteBehavior.Cascade);

        //    // ✅ RELATIONSHIP: Complaints → Users
        //    modelBuilder.Entity<Complaint>()
        //        .HasOne<User>()
        //        .WithMany()
        //        .HasForeignKey(c => c.UserId)
        //        .OnDelete(DeleteBehavior.Restrict);

        //    // ✅ RELATIONSHIP: Wallet → User
        //    modelBuilder.Entity<Wallet>()
        //        .HasOne<User>()
        //        .WithMany()
        //        .HasForeignKey(w => w.UserId)
        //        .OnDelete(DeleteBehavior.Cascade);

        //    // ✅ RELATIONSHIP: WalletTransaction → Wallet
        //    modelBuilder.Entity<WalletTransaction>()
        //        .HasOne<Wallet>()
        //        .WithMany()
        //        .HasForeignKey(wt => wt.WalletId)
        //        .OnDelete(DeleteBehavior.Cascade);
        //}
    }
}
