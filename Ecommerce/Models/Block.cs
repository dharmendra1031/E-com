using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    public class Block
    {
        [Key]
        public int Id { get; set; }
        public string BlockName { get; set; }
        public string BlockNameArb { get; set; }
        public int AreaId { get; set; }

        //[ForeignKey("AreaId")]
        //public virtual Area Area { get; set; }
    }
}
