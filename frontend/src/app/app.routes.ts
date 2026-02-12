import { Routes } from '@angular/router';
import { LoginComponent } from './login/login.component';
import { SignupComponent } from './signup/signup.component';
import { GroupsListComponent } from './groups-list/groups-list.component';
import { GroupDetailComponent } from './group-detail/group-detail.component';
import { authGuard } from './auth.guard';

export const routes: Routes = [
    { path: 'login', component: LoginComponent },
    { path: 'signup', component: SignupComponent },
    { path: 'groups', component: GroupsListComponent, canActivate: [authGuard] },
    { path: 'groups/:id', component: GroupDetailComponent, canActivate: [authGuard] },
    { path: '', redirectTo: '/groups', pathMatch: 'full' },
    { path: '**', redirectTo: '/groups' }
];
