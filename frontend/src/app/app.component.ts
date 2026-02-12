import { Component, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { AuthService } from './auth.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterModule, CommonModule],
  template: `
    <nav *ngIf="authService.isLoggedIn()" class="bg-gray-800 text-white p-4">
      <div class="container mx-auto flex justify-between">
        <a routerLink="/groups" class="font-bold text-xl">Tripper</a>
        <button (click)="authService.logout()" class="hover:text-gray-300">Logout</button>
      </div>
    </nav>
    <router-outlet></router-outlet>
  `
})
export class AppComponent {
  authService = inject(AuthService);
}
