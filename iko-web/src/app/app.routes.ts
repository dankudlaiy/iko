import { Routes } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { LoginComponent } from './login/login.component';
import { RegisterComponent } from './register/register.component';
import { SettingsComponent } from './settings/settings.component';
import { CallbackComponent } from './callback/callback.component';
import { LibraryComponent } from './library/library.component';
import { PlaylistEditorComponent } from './playlist-editor/playlist-editor.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'library', component: LibraryComponent, canActivate: [authGuard] },
  { path: 'library/playlist/:id', component: PlaylistEditorComponent, canActivate: [authGuard] },
  { path: 'settings', component: SettingsComponent, canActivate: [authGuard] },
  { path: 'callback/:platform', component: CallbackComponent },
  { path: '**', redirectTo: '' }
];
