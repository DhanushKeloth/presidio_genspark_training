export interface ShipmentQuoteResponse {
  baseRate: number;
  distanceCharge: number;
  weightSurcharge: number;
  platformFee: number;
  estimatedCost: number;
}