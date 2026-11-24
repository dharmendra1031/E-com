namespace EcommerceWebApplication.Models
{
    public class GuestRegisterRequestDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public int? AreaId { get; set; }
        public int? BlockId { get; set; }
        public string Street { get; set; }
        public string? HouseNo { get; set; }
        public string? Floor { get; set; }
        public string? ApartmentNo { get; set; }
        public int? ShareholderNo { get; set; }
        public string? Instruction { get; set; }
    }
}
