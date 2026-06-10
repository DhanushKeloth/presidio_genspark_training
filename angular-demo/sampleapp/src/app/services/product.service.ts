import { HttpClient } from "@angular/common/http";
import { map, Observable } from "rxjs";
import { Product } from "../models/product.model";
import { Injectable } from "@angular/core";
@Injectable({
    providedIn:'root'
})
export class ProductService{
    constructor(private http:HttpClient){}
    private getProductsurl = "https://dummyjson.com/products";
    getProducts(): Observable<Product[]> {
    return this.http
      .get<{ products: Product[] }>(this.getProductsurl)
      .pipe(
        map(response => response.products)
      );
    }

    getProductById(id:number):Observable<Product>{
        return this.http.get<Product>(`${this.getProductsurl}/${id}`)
    }
}