import { Routes } from '@angular/router';
import { Login } from './auth/pages/login/login';
import { RegisterCustomer } from './auth/pages/register-customer/register-customer';
import { RegisterDriver } from './auth/pages/register-driver/register-driver';

export const routes: Routes = [
    { path: '', redirectTo: 'auth/login', pathMatch: 'full' },
  
  // 2. Auth Routes
  { 
    path: 'auth', 
    children: [
        {path:'login',component:Login},
        {path:'register-customer',component:RegisterCustomer},
        {path:'register-driver',component:RegisterDriver},


    ]
  },
];
