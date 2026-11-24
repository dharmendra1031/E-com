namespace EcommerceWebApplication.Models
{
    public class BlogDto
    {
        public int BlogId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; } // If you want teaser text later
        public string BlogImageUrl { get; set; }
        public string Author { get; set; } = "Admin";
        public string Tag { get; set; } = "Services";
        public string Date { get; set; }
        public string Link { get; set; }
    }
}
