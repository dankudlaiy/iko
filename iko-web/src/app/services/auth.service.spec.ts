import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    localStorage.removeItem('iko_token');
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    http.verify();
    localStorage.removeItem('iko_token');
  });

  it('stores the token and emits the user on login', () => {
    let email: string | undefined;
    service.currentUser$.subscribe(u => email = u?.email);

    service.login('user@test.com', 'password123').subscribe();
    http.expectOne(`${environment.apiUrl}/auth/login`)
      .flush({ data: { token: 'jwt-token', email: 'user@test.com' }, error: null });

    expect(localStorage.getItem('iko_token')).toBe('jwt-token');
    expect(email).toBe('user@test.com');
    expect(service.isLoggedIn()).toBeTrue();
  });

  it('clears the token and navigates to /login on logout', () => {
    localStorage.setItem('iko_token', 'jwt-token');
    const navigate = spyOn(router, 'navigate');

    service.logout();

    expect(localStorage.getItem('iko_token')).toBeNull();
    expect(service.isLoggedIn()).toBeFalse();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });
});
