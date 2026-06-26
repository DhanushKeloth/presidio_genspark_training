import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { Navbar } from '../../../shared/components/navbar/navbar';
import { CustomerSidebar } from '../../../shared/components/customer-sidebar/customer-sidebar';

@Component({
  selector: 'app-customer-layout',
  imports: [RouterModule,Navbar,CustomerSidebar],
  templateUrl: './customer-layout.html',
  styleUrl: './customer-layout.css',
})
export class CustomerLayout {}
