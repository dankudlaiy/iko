import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-callback',
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule],
  templateUrl: './callback.component.html',
  styleUrls: ['./callback.component.css']
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

    this.http.get(`http://localhost:5000/api/accounts/callback/${platform}?code=${encodeURIComponent(code)}`)
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
