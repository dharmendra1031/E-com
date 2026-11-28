using System.Text.Json;
using EcommerceWebApplication.Models;

namespace EcommerceWebApplication.Helper;

// Immutable delivery data for marketplace orders; not exposed by legacy order entities or vendor APIs.
public sealed record SellerDeliverySnapshot(int AddressId, int AreaId, int BlockId,
    string? AreaName, string? AreaNameArb, string? BlockName, string? BlockNameArb,
    string? Street, string? HouseNo, string? Floor, string? Landmark, string? Phone,
    string? Governorate, string? City, string? State, string? ZipCode, string? Country)
{
    public static SellerDeliverySnapshot Capture(UserAddress address, Area area, Block block) =>
        new(address.AddressId, area.Id, block.Id, area.AreaName, area.AreaNameArb,
            block.BlockName, block.BlockNameArb, address.Street, address.HouseNo,
            address.Floor, address.Landmark, address.Phone, address.Governorate,
            address.City, address.State, address.ZipCode, address.Country);

    public string ToJson() => JsonSerializer.Serialize(this);

    public static SellerDeliverySnapshot? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var value = JsonSerializer.Deserialize<SellerDeliverySnapshot>(json);
            return value is { AddressId: > 0, AreaId: > 0, BlockId: > 0 } &&
                !string.IsNullOrWhiteSpace(value.Street) && !string.IsNullOrWhiteSpace(value.Phone) ? value : null;
        }
        catch (JsonException) { return null; }
    }
}
