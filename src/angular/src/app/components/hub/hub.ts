import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ChatMessage, GameHubService } from '../../services/game-hub-service';
import { LobbyNamePipe } from '../overview/pipes/lobby-name.pipe';
import { TranslocoPipe } from '@jsverse/transloco';
import { Chat } from '../chat/chat';
import { toSignal } from '@angular/core/rxjs-interop';
import { scan } from 'rxjs';
import { GameLobbyState } from '../../api';
import { Lobby } from '../lobby/lobby';
import { LoadingSpinnerComponent } from '../../common/loading-spinner/loading-spinner';
import { Game } from '../game/game';
import { PostGame } from '../post-game/post-game';

@Component({
  selector: 'app-hub',
  imports: [LobbyNamePipe, Chat, Lobby, LoadingSpinnerComponent, Game, PostGame],
  templateUrl: './hub.html',
  styleUrl: './hub.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Hub implements OnInit, OnDestroy {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private gameHubService = inject(GameHubService);
  public lobbyId = signal<string | null>(null);

  public gameLobbyState = GameLobbyState;

  public lobbyState = this.gameHubService.lobbyState.asReadonly();

  public chatMessages = toSignal(
    this.gameHubService.chatMessages$.pipe(
      scan((acc, curr) => [...acc, curr], [] as ChatMessage[]),
    ),
    { initialValue: [] },
  );

  async ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.lobbyId.set(id);

      try {
        console.log('Joining lobby');
        await this.gameHubService.joinLobby(id);
      } catch (err) {
        console.error('Failed to join lobby', err);
        this.router.navigateByUrl('/');
      }
    }
  }

  ngOnDestroy(): void {
    this.gameHubService
      .leaveLobby()
      .then(() => console.info('Lobby left'))
      .catch((err) => console.error('Failed to leave lobby on destroy', err));
  }

  async handleSendMessage(message: string) {
    await this.gameHubService.sendMessage(message);
  }
}
