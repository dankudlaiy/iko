import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../models';

export interface UserInfo {
  email: string;
  createdAt?: string;
}

export interface AuthResult {
  token: string;
  email: string;
}

const TOKEN_KEY = 'iko_token';
const API_URL = `${environment.apiUrl}/auth`;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private currentUserSubject = new BehaviorSubject<UserInfo | null>(null);
  currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient, private router: Router) {
    if (this.getToken()) {
      this.loadUser();
    }
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }

  register(email: string, password: string): Observable<ApiResponse<AuthResult>> {
    return this.http.post<ApiResponse<AuthResult>>(`${API_URL}/register`, { email, password }).pipe(
      tap(res => {
        if (res.data?.token) {
          localStorage.setItem(TOKEN_KEY, res.data.token);
          this.currentUserSubject.next({ email: res.data.email });
        }
      })
    );
  }

  login(email: string, password: string): Observable<ApiResponse<AuthResult>> {
    return this.http.post<ApiResponse<AuthResult>>(`${API_URL}/login`, { email, password }).pipe(
      tap(res => {
        if (res.data?.token) {
          localStorage.setItem(TOKEN_KEY, res.data.token);
          this.currentUserSubject.next({ email: res.data.email });
        }
      })
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    this.currentUserSubject.next(null);
    this.router.navigate(['/login']);
  }

  private loadUser(): void {
    this.http.get<ApiResponse<UserInfo>>(`${API_URL}/me`).subscribe({
      next: res => {
        if (res.data) {
          this.currentUserSubject.next(res.data);
        }
      },
      error: () => {
        localStorage.removeItem(TOKEN_KEY);
        this.currentUserSubject.next(null);
      }
    });
  }
}
