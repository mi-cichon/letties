import { Component, inject, signal, OnInit, OnDestroy, computed } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ChatMessage, GameHubService } from '../../services/game-hub-service';
import { LobbyNamePipe } from '../overview/pipes/lobby-name.pipe';
import { TranslocoPipe } from '@jsverse/transloco';
import { Chat } from '../chat/chat';
import { firstValueFrom, scan } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-lobby',
  templateUrl: './lobby.html',
  styleUrl: './lobby.scss',
  imports: [LobbyNamePipe, TranslocoPipe, Chat],
})
export class Lobby implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private gameHubService = inject(GameHubService);

  public lobbyId = signal<string | null>(null);
  public lobbyState = this.gameHubService.lobbyState.asReadonly();
  public chatMessages = toSignal(
    this.gameHubService.chatMessages$.pipe(
      scan((acc, curr) => [...acc, curr], [] as ChatMessage[]),
    ),
    { initialValue: [] },
  );

  public sortedSeats = computed(() => {
    const state = this.lobbyState();
    if (!state?.seats) return [];
    return [...state.seats].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  });

  async ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.lobbyId.set(id);

      try {
        console.log('Joining lobby');
        await this.gameHubService.joinLobby(id);
      } catch (err) {
        console.error('Failed to join lobby', err);
      }
    }
  }

  ngOnDestroy(): void {
    this.gameHubService
      .leaveLobby()
      .catch((err) => console.error('Failed to leave lobby on destroy', err));
  }

  getPlayerName(playerId: string): string {
    const player = this.lobbyState()?.players?.find((p) => p.playerId === playerId);
    return player?.playerName || 'Unknown Player';
  }

  getSpectators() {
    const seatedPlayerIds = this.lobbyState()?.seats?.map((s) => s.playerId) || [];
    return this.lobbyState()?.players?.filter((p) => !seatedPlayerIds.includes(p.playerId)) || [];
  }

  async handleSendMessage(message: string) {
    await this.gameHubService.sendMessage(message);
  }

  async onTakeSeat(seatId: string) {
    await this.gameHubService.enterSeat(seatId);
  }
}
