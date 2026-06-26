import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { BookShipmentRequest } from '../../models/shipment-models/BookShipment.model';
import { BookShipmentResponse } from '../../models/shipment-models/BookShipmentResponse.model';
import { ShipmentQuoteResponse } from '../../models/shipment-models/ShipmentQuoteResponse.model';

@Injectable({
  providedIn: 'root'
})
export class ShipmentService {

  constructor(private http: HttpClient) {}

  // --- PURE API CALLS ---

  public bookShipmentApi(shipmentData: BookShipmentRequest) {
    let url = environment.baseUrl + '/Shipments';
    return this.http.post<BookShipmentResponse>(url, shipmentData);
  }
  //returns the pricing details
  public getShipmentQuoteApi(shipmentData:BookShipmentRequest){
    let url = environment.baseUrl+'/Shipments/quote';
    return this.http.post<ShipmentQuoteResponse>(url,shipmentData);
  }
}