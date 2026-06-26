namespace ShipmentTrackingAPI.Models.DTOs.Common; // Or .Configuration

public class PricingSettings
{
    public decimal BaseRate { get; set; } 
    public decimal WeightRatePerKg { get; set; }
    public decimal DistanceRatePerKm { get; set; }
    public decimal DimWeightRate { get; set; }
    public decimal DimWeightDivisor { get; set; }
    public decimal PlatformFee { get; set; }
}