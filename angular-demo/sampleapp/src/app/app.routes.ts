import { Routes } from '@angular/router';
import { Login } from './login/login';
import { Dashboard } from './dashboard/dashboard';
import { Products } from './products/products';
import { ProductDetails } from './product-details/product-details';
import { authGuard } from './guards/authGuard';
import { Profile } from './profile/profile';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'dashboard', component: Dashboard,canActivate:[authGuard] },
  { path: 'products', component: Products,canActivate:[authGuard] },
  {
    path: 'products/:id',
    component: ProductDetails,
    canActivate: [authGuard],
  },
  {path:'profile',component:Profile,canActivate:[authGuard]}
];
