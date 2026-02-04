import { inject, Injectable, NgZone, OnDestroy, signal, WritableSignal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { getGameHubUrl } from '../core/utils/api-url-builder';
import {
  BotDifficulty,
  GameDetails,
  GameDetailsResult,
  GameLobbyItem,
  GameLobbyItemIReadOnlyListResult,
  JoinDetails,
  JoinDetailsResult,
  LobbySettingsModel,
  LobbyStateDetails,
  LobbyStateDetailsResult,
  MoveRequestModel,
  MoveResult,
  MoveResultResult,
  Result,
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
  private isDestroyed = false;
  private isVisibilityListenerRegistered = false;
  private currentLobbyId: string | null = null;

  private _connectionEstablished = signal(false);
  public connectionEstablished = this._connectionEstablished.asReadonly();

  private chatMessageSubject = new Subject<ChatMessage>();
  public chatMessages$ = this.chatMessageSubject.asObservable();

  public lobbyState: WritableSignal<LobbyStateDetails | null> = signal(null);
  public gameState: WritableSignal<GameDetails | null> = signal(null);

  public async initGameConnection(): Promise<boolean> {
    if (
      this.hubConnection &&
      (this.hubConnection.state === HubConnectionState.Connected ||
        this.hubConnection.state === HubConnectionState.Connecting ||
        this.hubConnection.state === HubConnectionState.Reconnecting)
    ) {
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

    this.hubConnection.onreconnecting(() => {
      this.ngZone.run(() => {
        console.warn('SignalR reconnecting...');
        this._connectionEstablished.set(false);
      });
    });

    this.hubConnection.onreconnected(() => {
      this.ngZone.run(() => {
        console.log('SignalR reconnected');
        this._connectionEstablished.set(true);
        this.refreshFullState(true);
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
    if (this.isVisibilityListenerRegistered) {
      return;
    }

    this.ngZone.runOutsideAngular(() => {
      document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
          this.ngZone.run(() => {
            console.log('Page visible, checking connection state:', this.hubConnection?.state);
            if (
              !this.hubConnection ||
              this.hubConnection.state === HubConnectionState.Disconnected
            ) {
              this.retryConnection();
            } else if (this.hubConnection.state === HubConnectionState.Connected) {
              this.refreshFullState(false);
            }
          });
        }
      });
    });

    this.isVisibilityListenerRegistered = true;
  }

  private async refreshFullState(forceRejoin = false) {
    if (this.hubConnection?.state === HubConnectionState.Connected) {
      console.log('Refreshing full state (forceRejoin:', forceRejoin, ')');

      if (forceRejoin && this.currentLobbyId) {
        try {
          console.log('Re-registering connection in lobby:', this.currentLobbyId);
          await this.joinLobby(this.currentLobbyId);
        } catch (err) {
          console.error('Failed to re-join lobby during refresh', err);
        }
      }

      this.getLobbyDetails().catch(() => {});
      this.getGameDetails().catch(() => {});
    }
  }

  private async retryConnection(): Promise<void> {
    if (this.isDestroyed || this.isRetrying) {
      return;
    }

    if (
      this.hubConnection &&
      (this.hubConnection.state === HubConnectionState.Connected ||
        this.hubConnection.state === HubConnectionState.Connecting ||
        this.hubConnection.state === HubConnectionState.Reconnecting)
    ) {
      return;
    }

    const token = getJwtToken();
    if (!token) {
      console.warn('No JWT token found, skipping retryConnection');
      return;
    }

    this.isRetrying = true;
    console.log('Retrying connection in 5s...');

    setTimeout(async () => {
      if (this.isDestroyed) return;

      console.log('Attempting to start connection...');
      try {
        if (!this.hubConnection || this.hubConnection.state === HubConnectionState.Disconnected) {
          if (!this.hubConnection) {
            this.isRetrying = false;
            await this.initGameConnection();
          } else {
            await this.hubConnection.start();
            console.log('Game Hub connected (reconnect)');
            this._connectionEstablished.set(true);
            this.isRetrying = false;
            this.refreshFullState(true);
          }
        } else {
          this.isRetrying = false;
        }
      } catch (err) {
        console.error('Retry connection failed', err);
        this.isRetrying = false;
        if (!this.isDestroyed) {
          this.retryConnection();
        }
      }
    }, 5000);
  }

  public getLobbies(): Promise<GameLobbyItem[]> {
    return this.hubConnection
      .invoke<GameLobbyItemIReadOnlyListResult>('GetLobbies')
      .then((result) => {
        if (result.isSuccess && result.value) {
          return result.value;
        }
        console.error('GetLobbies failed', result.error);
        return Promise.reject(result.error);
      })
      .catch((err) => {
        console.error('Error invoking GetLobbies: ', err);
        return Promise.reject(err);
      });
  }

  public joinLobby(lobbyId: string): Promise<JoinDetails> {
    this.currentLobbyId = lobbyId;
    return this.hubConnection
      .invoke<JoinDetailsResult>('Join', lobbyId)
      .then((result) => {
        if (result.isSuccess && result.value) {
          const response = result.value;
          this.ngZone.run(() => {
            if (response.lobbyState) {
              this.lobbyState.set(response.lobbyState);
            }
          });
          return response;
        }
        console.error('Join failed', result.error);
        return Promise.reject(result.error);
      })
      .catch((err) => {
        console.error('Error invoking Join: ', err);
        return Promise.reject(err);
      });
  }

  public leaveLobby(): Promise<void> {
    this.currentLobbyId = null;
    return this.hubConnection
      .invoke<Result>('LeaveLobby')
      .then((result) => {
        if (result.isSuccess) {
          this.ngZone.run(() => {
            this.lobbyState.set(null);
            this.gameState.set(null);
          });
          return;
        }
        console.error('LeaveLobby failed', result.error);
        return Promise.reject(result.error);
      })
      .catch((err) => {
        console.error('Error invoking LeaveLobby: ', err);
        return Promise.reject(err);
      });
  }

  public async sendMessage(message: string): Promise<void> {
    try {
      const result = await this.hubConnection.invoke<Result>('SendMessage', message);
      if (!result.isSuccess) {
        console.error('SendMessage failed', result.error);
        return Promise.reject(result.error);
      }
    } catch (err) {
      console.error('Error invoking SendMessage: ', err);
      return await Promise.reject(err);
    }
  }

  public async enterSeat(seatId: string): Promise<boolean> {
    try {
      const result = await this.hubConnection.invoke<Result>('EnterSeat', seatId);
      if (result.isSuccess) {
        return true;
      }
      console.error('EnterSeat failed', result.error);
      return false;
    } catch (err) {
      console.error('Error invoking EnterSeat: ', err);
      return false;
    }
  }

  public async leaveSeat(): Promise<boolean> {
    try {
      const result = await this.hubConnection.invoke<Result>('LeaveSeat');
      if (result.isSuccess) {
        return true;
      }
      console.error('LeaveSeat failed', result.error);
      return false;
    } catch (err) {
      console.error('Error invoking LeaveSeat: ', err);
      return false;
    }
  }

  public async updateSettings(settingsModel: LobbySettingsModel): Promise<void> {
    try {
      const result = await this.hubConnection.invoke<Result>('UpdateLobbySettings', settingsModel);
      if (!result.isSuccess) {
        console.error('UpdateLobbySettings failed', result.error);
        return Promise.reject(result.error);
      }
    } catch (err) {
      console.error('Error invoking UpdateLobbySettings: ', err);
      return await Promise.reject(err);
    }
  }

  public async addBot(seatId: string, botDifficulty: BotDifficulty): Promise<void> {
    try {
      const result = await this.hubConnection.invoke<Result>('AddBot', seatId, botDifficulty);
      if (!result.isSuccess) {
        console.error('AddBot failed', result.error);
        return Promise.reject(result.error);
      }
    } catch (err) {
      console.error('Error invoking AddBot: ', err);
      return await Promise.reject(err);
    }
  }

  public async removeBot(seatId: string): Promise<void> {
    try {
      const result = await this.hubConnection.invoke<Result>('RemoveBot', seatId);
      if (!result.isSuccess) {
        console.error('RemoveBot failed', result.error);
        return Promise.reject(result.error);
      }
    } catch (err) {
      console.error('Error invoking RemoveBot: ', err);
      return await Promise.reject(err);
    }
  }

  public async startGame(): Promise<void> {
    try {
      const result = await this.hubConnection.invoke<Result>('StartGame');
      if (!result.isSuccess) {
        console.error('StartGame failed', result.error);
        return Promise.reject(result.error);
      }
    } catch (err) {
      console.error('Error invoking StartGame: ', err);
      return await Promise.reject(err);
    }
  }

  public async getLobbyDetails(): Promise<void> {
    try {
      const result = await this.hubConnection.invoke<LobbyStateDetailsResult>('GetLobbyDetails');
      if (result.isSuccess && result.value) {
        const details = result.value;
        this.ngZone.run(() => {
          this.lobbyState.set(details);
        });
      } else if (result.isFailure && result.error?.code === 'Error.InvalidState') {
        // If we get InvalidState, it means the server doesn't think we're in a lobby.
        // We only clear if we aren't already trying to re-join in refreshFullState.
        // For now, let's just log it and see.
        console.warn('GetLobbyDetails: Player not in any lobby according to server.');
      } else {
        console.error('GetLobbyDetails failed', result.error);
      }
    } catch (err) {
      console.error('Error invoking GetLobbyDetails: ', err);
      return await Promise.reject(err);
    }
  }

  public async getGameDetails(): Promise<void> {
    try {
      const result = await this.hubConnection.invoke<GameDetailsResult>('GetGameDetails');
      if (result.isSuccess && result.value) {
        const details = result.value;
        this.ngZone.run(() => {
          this.gameState.set(details);
        });
      } else if (result.isFailure && result.error?.code === 'Error.InvalidState') {
        // Only clear game state if we are sure we're not in a game.
        // During reconnect, this might be transient.
        if (this.gameState()) {
          this.ngZone.run(() => this.gameState.set(null));
        }
      } else {
        console.error('GetGameDetails failed', result.error);
      }
    } catch (err) {
      console.error('Error invoking GetGameDetails: ', err);
      return await Promise.reject(err);
    }
  }

  public async handleMove(request: MoveRequestModel): Promise<MoveResult> {
    try {
      const result = await this.hubConnection.invoke<MoveResultResult>('HandleMove', request);
      if (result.isSuccess && result.value) {
        return result.value;
      }
      console.error('HandleMove failed', result.error);
      return Promise.reject(result.error);
    } catch (err) {
      console.error('Error invoking HandleMove: ', err);
      return await Promise.reject(err);
    }
  }

  public async skipTurn(): Promise<void> {
    try {
      const result = await this.hubConnection.invoke<Result>('SkipTurn');
      if (!result.isSuccess) {
        console.error('SkipTurn failed', result.error);
        return Promise.reject(result.error);
      }
    } catch (err) {
      console.error('Error invoking SkipTurn: ', err);
      return await Promise.reject(err);
    }
  }

  public async swapTiles(request: string[]): Promise<void> {
    try {
      const result = await this.hubConnection.invoke<Result>('SwapTiles', request);
      if (!result.isSuccess) {
        console.error('SwapTiles failed', result.error);
        return Promise.reject(result.error);
      }
    } catch (err) {
      console.error('Error invoking SwapTiles: ', err);
      return await Promise.reject(err);
    }
  }

  ngOnDestroy(): void {
    this.isDestroyed = true;
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
