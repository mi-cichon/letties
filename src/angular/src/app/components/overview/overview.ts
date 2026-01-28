import { Component, inject, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslocoModule } from '@jsverse/transloco';
import { Router } from '@angular/router';
import { GameHubService } from '../../services/game-hub-service';
import { from } from 'rxjs';
import { LobbyNamePipe } from './pipes/lobby-name.pipe';
import { LoadingSpinnerComponent } from '../../common/loading-spinner/loading-spinner';

@Component({
  selector: 'app-overview',
  standalone: true,
  imports: [CommonModule, TranslocoModule, LobbyNamePipe, LoadingSpinnerComponent],
  templateUrl: './overview.html',
  styleUrl: './overview.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Overview {
  private readonly gameHubService = inject(GameHubService);
  private readonly router = inject(Router);

  lobbiesResource = rxResource({
    stream: () => from(this.gameHubService.getLobbies()),
  });

  joinLobby(lobbyId: string | undefined) {
    console.log(lobbyId);
    if (!lobbyId) return;
    this.router.navigate(['/lobby', lobbyId]);
  }

  refresh() {
    this.lobbiesResource.reload();
  }
}
