import { Component, inject, Signal, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LoginHubService } from '../../services/login-hub-service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslocoPipe],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private router = inject(Router);
  private loginHubService = inject(LoginHubService);

  username = signal('');
  isLoading = signal(false);

  onSubmit() {
    if (this.username().length < 3) return;

    this.isLoading.set(true);

    this.loginHubService.login(this.username()).then(() => {
      this.router.navigate(['/overview']);
    });
  }
}
