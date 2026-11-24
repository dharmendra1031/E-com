namespace EcommerceWebApplication.Models.ViewModels
{
    public class SingleReviewDto
    {

        public int ReviewId { get; set; }
        public int ProductId { get; set; }
        public int UserId { get; set; }
        // optional, agar user ka naam show karna ho
        public int Rating { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
