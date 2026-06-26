export interface ShipmentItemRequest {
  description: string;
  weight: number;
  quantity: number;
  height?: number | null;
  width?: number | null;
  length?: number | null;
}

export interface BookShipmentRequest {
  pickupAddress: string;
  pickupLat?: number | null;
  pickupLng?: number | null;

  dropoffAddress: string;
  dropoffLat?: number | null;
  dropoffLng?: number | null;

  recipientName: string;
  recipientPhone: string;

  items: ShipmentItemRequest[];
}