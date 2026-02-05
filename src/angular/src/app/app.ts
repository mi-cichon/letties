import { Component, computed, effect, inject, Signal, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LoginHubService } from './services/login-hub-service';
import { toSignal } from '@angular/core/rxjs-interop';
import { from } from 'rxjs';
import { LoadingSpinnerComponent } from './common/loading-spinner/loading-spinner';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterOutlet, TranslocoPipe, LoadingSpinnerComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  public readonly loginHubService = inject(LoginHubService);
  private readonly router = inject(Router);

  public $initiateLoginHub: Signal<boolean> = toSignal(
    from(this.loginHubService.initLoginConnection()),
    { initialValue: false },
  );

  private translocoService = inject(TranslocoService);

  protected activeLang = toSignal(this.translocoService.langChanges$, {
    initialValue: this.translocoService.getActiveLang(),
  });

  goHome() {
    this.router.navigate(['/overview']);
  }

  setLang(lang: string) {
    this.translocoService.setActiveLang(lang);
    localStorage.setItem(environment.selectedLangStorageKey, lang);
  }
}
