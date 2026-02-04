import { inject, Injectable, NgZone, OnDestroy, signal, WritableSignal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { getGameHubUrl } from '../core/utils/api-url-builder';
import {
  BotDifficulty,
  GameDetails,
  GameLobbyItem,
  LobbySettingsModel,
  LobbyStateDetails,
  MoveRequestModel,
  MoveResult,
} from '../api';
import { getJwtToken } from '../core/utils/token-utils';
import { Subject } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class GameHubService implements OnDestroy {
  private hubConnection!: HubConnection;
  private ngZone = inject(NgZone);
  private isRetrying = false;

  private _connectionEstablished = signal(false);
  public connectionEstablished = this._connectionEstablished.asReadonly();

  private chatMessageSubject = new Subject<ChatMessage>();
  public chatMessages$ = this.chatMessageSubject.asObservable();

  public lobbyState: WritableSignal<LobbyStateDetails | null> = signal(null);
  public gameState: WritableSignal<GameDetails | null> = signal(null);

  public async initGameConnection(): Promise<boolean> {
    if (this.hubConnection && this.hubConnection.state !== HubConnectionState.Disconnected) {
      return true;
    }

    this.hubConnection = new HubConnectionBuilder()
      .withUrl(getGameHubUrl(), {
        accessTokenFactory: () => {
          const token = getJwtToken();
          return token || '';
        },
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
      .build();

    try {
      this.registerHandlers();
      await this.hubConnection.start();
      console.log('Game Hub connected');
      this._connectionEstablished.set(true);
      this.handlePageVisibility();
      return true;
    } catch (err) {
      console.error('Error connecting to Game Hub:', err);
      this._connectionEstablished.set(false);
      this.retryConnection();
      return false;
    }
  }

  private registerHandlers() {
    this.hubConnection.on('ReceiveMessage', (playerName: string, message: string) => {
      this.ngZone.run(() => {
        const newMessage: ChatMessage = {
          author: playerName,
          text: message,
          timestamp: new Date(),
        };
        this.chatMessageSubject.next(newMessage);
      });
    });

    this.hubConnection.on('LobbyUpdated', (lobbyState: LobbyStateDetails) => {
      this.ngZone.run(() => {
        this.lobbyState.set(lobbyState);
        console.info('Lobby updated', lobbyState);
      });
    });

    this.hubConnection.on('GameUpdated', (gameState: GameDetails) => {
      this.ngZone.run(() => {
        this.gameState.set(gameState);
        console.info('Game updated', gameState);
      });
    });

    this.hubConnection.onreconnected(() => {
      this.ngZone.run(() => {
        console.log('SignalR reconnected');
        this.refreshFullState();
      });
    });

    this.hubConnection.onclose(() => {
      this.ngZone.run(() => {
        console.log('SignalR connection closed');
        this._connectionEstablished.set(false);
        this.retryConnection();
      });
    });
  }

  private handlePageVisibility() {
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') {
        if (this.hubConnection.state === HubConnectionState.Disconnected && !this.isRetrying) {
          this.retryConnection();
        } else if (this.hubConnection.state === HubConnectionState.Connected) {
          this.refreshFullState();
        }
      }
    });
  }

  private refreshFullState() {
    if (this.hubConnection.state === HubConnectionState.Connected) {
      this.getLobbyDetails();
      this.getGameDetails();
    }
  }

  private async retryConnection(): Promise<void> {
    if (this.isRetrying || this.hubConnection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.isRetrying = true;
    console.log('Retrying connection...');

    try {
      await this.hubConnection.start();
      console.log('Game Hub connected (reconnect)');
      this._connectionEstablished.set(true);
      this.isRetrying = false;
      this.refreshFullState();
    } catch (err) {
      console.error('Retry connection failed', err);
      this.isRetrying = false;
      setTimeout(() => this.retryConnection(), 5000);
    }
  }

  public getLobbies(): Promise<GameLobbyItem[]> {
    return this.hubConnection
      .invoke('GetLobbies')
      .then((items: GameLobbyItem[]) => items)
      .catch((err) => {
        console.error('Error invoking GetLobbies: ', err);
        return Promise.reject();
      });
  }

  public joinLobby(lobbyId: string): Promise<JoinResponse> {
    return this.hubConnection
      .invoke('Join', lobbyId)
      .then((response: JoinResponse) => {
        this.ngZone.run(() => {
          this.lobbyState.set(response.lobbyState!);
        });
        return Promise.resolve(response);
      })
      .catch((err) => {
        console.error('Error invoking Join: ', err);
        return Promise.reject();
      });
  }

  public leaveLobby(): Promise<void> {
    return this.hubConnection
      .invoke('LeaveLobby')
      .then(() => {
        this.ngZone.run(() => {
          this.lobbyState.set(null);
        });
      })
      .catch((err) => {
        console.error('Error invoking LeaveLobby: ', err);
        return Promise.reject();
      });
  }

  public async sendMessage(message: string): Promise<void> {
    try {
      return await this.hubConnection.invoke('SendMessage', message);
    } catch (err) {
      console.error('Error invoking SendMessage: ', err);
      return await Promise.reject();
    }
  }

  public async enterSeat(seatId: string): Promise<boolean> {
    try {
      return await this.hubConnection.invoke('EnterSeat', seatId);
    } catch (err) {
      console.error('Error invoking EnterSeat: ', err);
      return await Promise.reject();
    }
  }

  public async leaveSeat(): Promise<boolean> {
    try {
      return await this.hubConnection.invoke('LeaveSeat');
    } catch (err) {
      console.error('Error invoking LeaveSeat: ', err);
      return await Promise.reject();
    }
  }

  public async updateSettings(settingsModel: LobbySettingsModel): Promise<void> {
    try {
      return await this.hubConnection.invoke('UpdateLobbySettings', settingsModel);
    } catch (err) {
      console.error('Error invoking UpdateLobbySettings: ', err);
      return await Promise.reject();
    }
  }

  public async addBot(seatId: string, botDifficulty: BotDifficulty): Promise<void> {
    try {
      return await this.hubConnection.invoke('AddBot', seatId, botDifficulty);
    } catch (err) {
      console.error('Error invoking AddBot: ', err);
      return await Promise.reject();
    }
  }

  public async removeBot(seatId: string): Promise<void> {
    try {
      return await this.hubConnection.invoke('RemoveBot', seatId);
    } catch (err) {
      console.error('Error invoking RemoveBot: ', err);
      return await Promise.reject();
    }
  }

  public async startGame(): Promise<void> {
    try {
      return await this.hubConnection.invoke('StartGame');
    } catch (err) {
      console.error('Error invoking StartGame: ', err);
      return await Promise.reject();
    }
  }

  public async getLobbyDetails(): Promise<void> {
    try {
      const details = await this.hubConnection.invoke('GetLobbyDetails');
      this.ngZone.run(() => {
        this.lobbyState.set(details);
      });
    } catch (err) {
      console.error('Error invoking GetLobbyDetails: ', err);
      return await Promise.reject();
    }
  }

  public async getGameDetails(): Promise<void> {
    try {
      const details = await this.hubConnection.invoke('GetGameDetails');
      this.ngZone.run(() => {
        this.gameState.set(details);
      });
    } catch (err) {
      console.error('Error invoking GetGameDetails: ', err);
      return await Promise.reject();
    }
  }

  public async handleMove(request: MoveRequestModel): Promise<MoveResult> {
    try {
      return await this.hubConnection.invoke('HandleMove', request);
    } catch (err) {
      console.error('Error invoking HandleMove: ', err);
      return await Promise.reject();
    }
  }

  public async skipTurn(): Promise<void> {
    try {
      return await this.hubConnection.invoke('SkipTurn');
    } catch (err) {
      console.error('Error invoking SkipTurn: ', err);
      return await Promise.reject();
    }
  }

  public async swapTiles(request: string[]): Promise<void> {
    try {
      return await this.hubConnection.invoke('SwapTiles', request);
    } catch (err) {
      console.error('Error invoking SwapTiles: ', err);
      return await Promise.reject();
    }
  }

  ngOnDestroy(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
  }
}

export interface ChatMessage {
  author: string;
  text: string;
  timestamp: Date;
}
