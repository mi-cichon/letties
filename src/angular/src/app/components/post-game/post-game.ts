import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  OnDestroy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { GameHubService } from '../../services/game-hub-service';
import { TranslocoModule } from '@jsverse/transloco';
import { GetBotInfoPipe } from '../lobby/pipes/get-bot-info-pipe';

@Component({
  selector: 'app-post-game',
  standalone: true,
  imports: [CommonModule, TranslocoModule, GetBotInfoPipe],
  templateUrl: './post-game.html',
  styleUrl: './post-game.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PostGame implements OnDestroy {
  private gameHub = inject(GameHubService);

  public lobbyState = this.gameHub.lobbyState.asReadonly();
  public timeLeft = signal<number>(0);
  private timerInterval?: any;

  public sortedPlayers = computed(() => {
    const players = this.lobbyState()?.gameFinishedDetails?.players || [];
    return [...players].sort((a, b) => (b.playerPoints ?? 0) - (a.playerPoints ?? 0));
  });

  constructor() {
    this.startCountdown();
  }

  private startCountdown() {
    const details = this.lobbyState()?.gameFinishedDetails;
    if (!details) return;

    const finishedAt = new Date(details.finishedAt!).getTime();
    const durationMs = details.postGameDurationSeconds! * 1000;
    const endTime = finishedAt + durationMs;

    this.timerInterval = setInterval(() => {
      const now = Date.now();
      const remaining = Math.max(0, Math.floor((endTime - now) / 1000));
      this.timeLeft.set(remaining);

      if (remaining <= 0) {
        clearInterval(this.timerInterval);
      }
    }, 1000);
  }

  ngOnDestroy() {
    if (this.timerInterval) clearInterval(this.timerInterval);
  }
}
