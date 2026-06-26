import { Routes } from '@angular/router';
import { HomeComponent } from './home-component/home-component';
import { AboutComponent } from './about-component/about-component';

export const routes: Routes = [
  { path: 'home', component: HomeComponent },
  { path: 'about', component: AboutComponent },
  {
    path: 'login',
    loadComponent: () => 
        //fake the network delay by 3 sec for the spinner to appear until the component loads
      new Promise(resolve => setTimeout(resolve, 3000)) // 3000ms = 3 seconds
        .then(() => import('./login/login'))
        .then(c => c.Login)
  },

];
