import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { Router } from '@angular/router';

export interface UserInfo {
  email: string;
  createdAt?: string;
}

const TOKEN_KEY = 'iko_token';
const API_URL = 'http://localhost:5000/api/auth';

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

  register(email: string, password: string): Observable<any> {
    return this.http.post<any>(`${API_URL}/register`, { email, password }).pipe(
      tap(res => {
        if (res.data?.token) {
          localStorage.setItem(TOKEN_KEY, res.data.token);
          this.currentUserSubject.next({ email: res.data.email });
        }
      })
    );
  }

  login(email: string, password: string): Observable<any> {
    return this.http.post<any>(`${API_URL}/login`, { email, password }).pipe(
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
    this.http.get<any>(`${API_URL}/me`).subscribe({
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
