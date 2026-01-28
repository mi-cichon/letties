import { inject, Injectable, signal, WritableSignal } from '@angular/core';
import { Router } from '@angular/router';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { getGameHubUrl } from '../core/utils/api-url-builder';
import { GameLobbyItem, JoinResponse, LobbySeatDetails, LobbyStateDetails } from '../api';
import { getJwtToken } from '../core/utils/token-utils';
import { Subject } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class GameHubService {
  private hubConnection!: HubConnection;

  private _connectionEstablished = signal(false);
  public connectionEstablished = this._connectionEstablished.asReadonly();

  private chatMessageSubject = new Subject<ChatMessage>();
  public chatMessages$ = this.chatMessageSubject.asObservable();

  public lobbyState: WritableSignal<LobbyStateDetails | null> = signal(null);

  public async initGameConnection(): Promise<boolean> {
    this.hubConnection = new HubConnectionBuilder()
      .withUrl(getGameHubUrl(), {
        accessTokenFactory: () => {
          const token = getJwtToken();
          return token;
        },
      })
      .withAutomaticReconnect()
      .build();

    try {
      await this.hubConnection.start();
      console.log('Game Hub connected');
      this._connectionEstablished.set(true);
      this.registerHandlers();
      return true;
    } catch (err) {
      console.error('Error connecting to Game Hub:', err);
      this._connectionEstablished.set(false);
      return false;
    }
  }

  private registerHandlers() {
    this.hubConnection.on('ReceiveMessage', (playerName: string, message: string) => {
      const newMessage: ChatMessage = {
        author: playerName,
        text: message,
        timestamp: new Date(),
      };

      this.chatMessageSubject.next(newMessage);
    });

    this.hubConnection.on('PlayerEnteredSeat', (s) => this.updateSeatInState(s));
    this.hubConnection.on('PlayerLeftSeat', (s) => this.updateSeatInState(s));
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
        this.lobbyState.set(response.lobbyState!);
        return Promise.resolve(response);
      })
      .catch((err) => {
        console.error('Error invoking Join: ', err);
        return Promise.reject();
      });
  }

  public leaveLobby(): Promise<JoinResponse> {
    return this.hubConnection
      .invoke('LeaveLobby')
      .then()
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

  private updateSeatInState(seatUpdate: LobbySeatDetails) {
    console.log('seat updated', seatUpdate);
    this.lobbyState.update((state) => {
      if (!state) return null;
      return {
        ...state,
        seats: state.seats!.map((s) => (s.seatId === seatUpdate.seatId ? seatUpdate : s)),
      };
    });
  }
}

export interface ChatMessage {
  author: string;
  text: string;
  timestamp: Date;
}
