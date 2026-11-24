using EcommerceWebApplication.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("ProductStock")]   // matches SQL table
public class ProductStock
{
    [Key]
    public int ID { get; set; }

    public int? ProductId { get; set; }

 

    public int? StockQty { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int CreatedBy { get; set; }

    [ForeignKey("ProductId")]
    public Products? Product { get; set; }
}
