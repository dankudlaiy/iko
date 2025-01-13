import {Component, OnInit} from '@angular/core';
import {ActivatedRoute, Router} from "@angular/router";
import {MainService} from "../../../services/main.service";
import {ButtonDirective} from "primeng/button";
@Component({
  selector: 'app-spotify-callback',
  standalone: true,
  imports: [],
  templateUrl: './spotify-callback.component.html',
  styleUrl: './spotify-callback.component.css'
})
export class SpotifyCallbackComponent implements OnInit {
  constructor(private route: ActivatedRoute, private mainService: MainService, private router: Router) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const authToken = params['code'];

      if (authToken) {
        this.mainService.obtainAccessToken(authToken).subscribe(response => {
          const accessToken = response.access_token;
          localStorage.setItem('access_token', accessToken);
          this.router.navigate(['/']);
        });
      }
    });
  }

  goBack() {
    this.router.navigate(['']);
  }
}
