import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { HlmSpinner } from '@spartan-ng/helm/spinner';
import { environment } from '../../environments/environment';

@Component({
    selector: 'app-callback',
    imports: [RouterLink, HlmSpinner],
    templateUrl: './callback.component.html',
})
export class CallbackComponent implements OnInit {
  loading = true;
  errorMessage = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private http: HttpClient
  ) {}

  ngOnInit(): void {
    const platform = this.route.snapshot.paramMap.get('platform');
    const code = this.route.snapshot.queryParamMap.get('code');

    if (!platform || !code) {
      this.errorMessage = 'Missing platform or authorization code';
      this.loading = false;
      return;
    }

    this.http.get(`${environment.apiUrl}/accounts/callback/${platform}?code=${encodeURIComponent(code)}`)
      .subscribe({
        next: (res: any) => {
          if (res.data?.access_token) {
            localStorage.setItem('access_token', res.data.access_token);
          }
          this.loading = false;
          this.router.navigate(['/settings']);
        },
        error: (err) => {
          this.errorMessage = err.error?.error || 'Authorization failed';
          this.loading = false;
        }
      });
  }
}
