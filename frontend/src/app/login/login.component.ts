import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../auth.service';

@Component({
    selector: 'app-login',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    template: `
    <div class="min-h-screen flex items-center justify-center bg-gray-100">
      <div class="bg-white p-8 rounded shadow-md w-full max-w-md">
        <h2 class="text-2xl font-bold mb-6 text-center">Login to Tripper</h2>
        <form (ngSubmit)="onSubmit()">
          <div class="mb-4">
            <label class="block text-gray-700 mb-2">Email</label>
            <input [(ngModel)]="email" name="email" type="email" class="w-full p-2 border rounded" required>
          </div>
          <div class="mb-6">
            <label class="block text-gray-700 mb-2">Password</label>
            <input [(ngModel)]="password" name="password" type="password" class="w-full p-2 border rounded" required>
          </div>
          <button type="submit" class="w-full bg-blue-500 text-white p-2 rounded hover:bg-blue-600">Login</button>
        </form>
        <p class="mt-4 text-center">
          Don't have an account? <a routerLink="/signup" class="text-blue-500">Sign up</a>
        </p>
      </div>
    </div>
  `
})
export class LoginComponent {
    email = '';
    password = '';
    private authService = inject(AuthService);
    private router = inject(Router);

    onSubmit() {
        this.authService.login({ email: this.email, password: this.password }).subscribe({
            next: () => this.router.navigate(['/groups']),
            error: (err) => alert('Login failed')
        });
    }
}
