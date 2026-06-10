import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { AuthService } from "../services/auth.service";

export const authGuard:CanActivateFn = () => {
    const router = inject(Router);
    const authservice = inject(AuthService);
    const userStatus = authservice.isLoggedIn();
    if (userStatus) 
        return true;
    router.navigate(["/login"]);
    return false;
}


