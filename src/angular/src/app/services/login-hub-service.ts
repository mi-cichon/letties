import { inject, Injectable, NgZone, signal, Signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { getLoginHubUrl } from '../core/utils/api-url-builder';
import { environment } from '../../environments/environment';
import { Router } from '@angular/router';
import { GameHubService } from './game-hub-service';
import { TranslocoService } from '@jsverse/transloco';
import { getSelectedLang } from '../core/utils/token-utils';
import { LoginData } from '../api';

@Injectable({
  providedIn: 'root',
})
export class LoginHubService {
  private hubConnection!: HubConnection;

  private readonly router = inject(Router);
  private readonly gameHubService = inject(GameHubService);
  private readonly translocoService = inject(TranslocoService);
  private readonly ngZone = inject(NgZone);

  private _connectionEstablished = signal(false);
  public connectionEstablished = this._connectionEstablished.asReadonly();

  private _loggedIn = signal(false);
  public loggedIn = this._loggedIn.asReadonly();

  private isRetrying = false;

  public initLoginConnection(): Promise<boolean> {
    if (this.hubConnection && this.hubConnection.state !== HubConnectionState.Disconnected) {
      return Promise.resolve(true);
    }

    this.hubConnection = new HubConnectionBuilder()
      .withUrl(getLoginHubUrl())
      .withAutomaticReconnect()
      .build();

    this.hubConnection.onclose(() => {
      this.ngZone.run(() => {
        console.log('Login Hub connection closed');
        this._connectionEstablished.set(false);
        this.retryConnection();
      });
    });

    return this.hubConnection
      .start()
      .then(() => {
        console.log('Login Hub connected');
        this._connectionEstablished.set(true);
        return true;
      })
      .catch((err) => {
        console.error('Error connecting to Login Hub:', err);
        this._connectionEstablished.set(false);
        this.retryConnection();
        return false;
      });
  }

  private async retryConnection(): Promise<void> {
    if (
      this.isRetrying ||
      !this.hubConnection ||
      this.hubConnection.state !== HubConnectionState.Disconnected
    ) {
      return;
    }

    this.isRetrying = true;
    console.log('Retrying Login Hub connection...');
    try {
      await this.hubConnection.start();
      console.log('Login Hub connected (reconnect)');
      this._connectionEstablished.set(true);
      this.isRetrying = false;
    } catch (err) {
      console.error('Login Hub retry connection failed', err);
      this.isRetrying = false;
      setTimeout(() => this.retryConnection(), 5000);
    }
  }

  public login(username: string): Promise<void> {
    return this.hubConnection
      .invoke('Login', username)
      .then((loginData: LoginData) => {
        if (loginData) {
          console.log('Logged in successfully');
          localStorage.setItem(environment.jwtStorageKey, loginData.jwtToken!);
          localStorage.setItem(environment.nicknameStorageKey, loginData.username!);
          localStorage.setItem(environment.playerIdStorageKey, loginData.playerId!);
          localStorage.setItem(
            environment.selectedLangStorageKey,
            this.translocoService.getActiveLang(),
          );
          this._loggedIn.set(true);
        } else {
          this._loggedIn.set(false);
          console.log('Login error!');
        }
      })
      .catch((err) => {
        console.error('Error invoking login: ', err);
        this._loggedIn.set(false);
      });
  }

  public async checkAuthorization(): Promise<boolean> {
    const token = localStorage.getItem(environment.jwtStorageKey);
    const user = localStorage.getItem(environment.nicknameStorageKey);
    const playerId = localStorage.getItem(environment.playerIdStorageKey);

    if (!token || !user || !playerId) {
      this._loggedIn.set(false);
      return Promise.resolve(false);
    }

    const isValid = await this.hubConnection.invoke<boolean>('Validate', token);

    if (isValid) {
      this._loggedIn.set(true);
      this.translocoService.setActiveLang(getSelectedLang());
      await this.gameHubService.initGameConnection();
    }

    return isValid;
  }

  public logout(): void {
    localStorage.removeItem(environment.jwtStorageKey);
    localStorage.removeItem(environment.nicknameStorageKey);
    this._loggedIn.set(false);
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
    this.router.navigate(['/login']);
  }
}
