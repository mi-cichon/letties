import { Component, inject, signal, OnInit, OnDestroy, computed, input } from '@angular/core';
import { GameHubService } from '../../services/game-hub-service';
import { TranslocoPipe } from '@jsverse/transloco';
import { getPlayerId } from '../../core/utils/token-utils';
import { BoardType, BotDifficulty, GameLanguage } from '../../api';
import { IsBotPipe } from './pipes/is-bot-pipe';

@Component({
  selector: 'app-lobby',
  templateUrl: './lobby.html',
  styleUrl: './lobby.scss',
  imports: [TranslocoPipe, IsBotPipe],
})
export class Lobby {
  private gameHubService = inject(GameHubService);

  public lobbyId = input.required<string>();
  public lobbyState = this.gameHubService.lobbyState.asReadonly();

  public sortedSeats = computed(() => {
    const state = this.lobbyState();
    if (!state?.seats) return [];
    return [...state.seats].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  });

  public gameLanguage = GameLanguage;
  public boardType = BoardType;
  public parseInt = parseInt;

  public myId = signal<string>(getPlayerId());

  public amIAdmin = computed(() => {
    const state = this.lobbyState();
    if (!state?.seats) return false;

    const firstSeat = state.seats.find((s) => s.order === 1);
    return firstSeat?.playerId === this.myId();
  });

  public seatedPlayersCount = computed(() => {
    return this.lobbyState()?.seats?.filter((s) => s.playerId !== null).length ?? 0;
  });

  public canStartGame = computed(() => {
    return this.amIAdmin() && this.seatedPlayersCount() >= 2;
  });

  getPlayerName(playerId: string): string {
    const player = this.lobbyState()?.players?.find((p) => p.playerId === playerId);
    return player?.playerName || 'Unknown Player';
  }

  getSpectators() {
    const seatedPlayerIds = this.lobbyState()?.seats?.map((s) => s.playerId) || [];
    return this.lobbyState()?.players?.filter((p) => !seatedPlayerIds.includes(p.playerId)) || [];
  }

  async onTakeSeat(seatId: string) {
    await this.gameHubService.enterSeat(seatId);
  }

  async updateSetting(key: string, value: any) {
    const currentSettings = this.lobbyState()?.settings;
    const newSettings = {
      ...currentSettings,
      [key]: value,
    };
    console.info(newSettings);
    if (currentSettings) {
      await this.gameHubService.updateSettings(newSettings);
    }
  }

  async onStartGame() {
    await this.gameHubService.startGame();
  }

  async onAddBot(seatId: string) {
    await this.gameHubService.addBot(seatId, BotDifficulty.Easy);
  }

  async onRemoveBot(seatId: string) {
    await this.gameHubService.removeBot(seatId);
  }
}
