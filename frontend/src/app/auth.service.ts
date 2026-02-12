import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { Router } from '@angular/router';

@Injectable({
    providedIn: 'root'
})
export class AuthService {
    private http = inject(HttpClient);
    private router = inject(Router);
    private apiUrl = 'http://localhost:5208/auth'; // Adjust port if needed

    login(credentials: any): Observable<any> {
        return this.http.post(`${this.apiUrl}/login`, credentials).pipe(
            tap((res: any) => {
                localStorage.setItem('token', res.token);
                localStorage.setItem('userId', res.userId);
                localStorage.setItem('username', res.username);
            })
        );
    }

    signup(data: any): Observable<any> {
        return this.http.post(`${this.apiUrl}/signup`, data).pipe(
            tap((res: any) => {
                localStorage.setItem('token', res.token);
                localStorage.setItem('userId', res.userId);
                localStorage.setItem('username', res.username);
            })
        );
    }

    logout() {
        localStorage.removeItem('token');
        localStorage.removeItem('userId');
        localStorage.removeItem('username');
        this.router.navigate(['/login']);
    }

    getToken() {
        return localStorage.getItem('token');
    }

    isLoggedIn() {
        return !!this.getToken();
    }
}
