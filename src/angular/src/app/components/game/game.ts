import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { GameDetails, LobbyPlayerDetails, MoveRequestModel, MoveResult } from '../../api';
import { GameHubService } from '../../services/game-hub-service';
import { LoadingSpinnerComponent } from '../../common/loading-spinner/loading-spinner';
import { TranslocoPipe } from '@jsverse/transloco';
import { PlayerNamePipe } from './pipes/player-name-pipe';
import { CellLabelPipe } from './pipes/cell-label-pipe';
import { getPlayerId } from '../../core/utils/token-utils';
import { SlicePipe } from '@angular/common';

@Component({
  selector: 'app-game',
  imports: [LoadingSpinnerComponent, TranslocoPipe, PlayerNamePipe, CellLabelPipe, SlicePipe],
  templateUrl: './game.html',
  styleUrl: './game.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Game {
  lobbyId = input.required<string>();
  players = input.required<LobbyPlayerDetails[]>();
  myId = signal(getPlayerId());

  private gameHubService = inject(GameHubService);

  gameState = this.gameHubService.gameState.asReadonly();

  placedTilesMap = computed(() => {
    const map = new Map<string, any>();
    this.gameState()?.boardContent?.placedTiles?.forEach((tile) => {
      map.set(tile.cellId!, tile);
    });
    return map;
  });

  tileDefsMap = computed(() => {
    const map = new Map<string, any>();
    this.gameState()?.tileDefinitions?.forEach((def) => {
      map.set(def.valueId!, def);
    });
    return map;
  });

  lastError = signal<string | null>(null);

  selectedTileId = signal<string | null>(null);
  localPlacements = signal<Map<string, string>>(new Map());

  usedTileIds = computed(() => new Set(this.localPlacements().values()));

  isMyTurn = computed(() => this.gameState()?.currentTurnPlayerId === this.myId());

  getTileValueIdFromHand(tileId: string | null): string | undefined {
    if (!tileId) return undefined;
    return this.gameState()?.myHand?.tiles?.find((t) => t.tileId === tileId)?.valueId;
  }

  async ngOnInit() {
    await this.gameHubService.getGameDetails();
  }

  onTileSelect(tileId: string) {
    if (this.usedTileIds().has(tileId) || !this.isMyTurn()) return;
    this.selectedTileId.set(tileId);
  }

  onCellClick(cellId: string) {
    const selectedId = this.selectedTileId();
    const isOccupiedByServer = this.placedTilesMap().has(cellId);

    if (isOccupiedByServer) return;

    if (this.localPlacements().has(cellId)) {
      const newMap = new Map(this.localPlacements());
      newMap.delete(cellId);
      this.localPlacements.set(newMap);
      return;
    }

    if (selectedId) {
      const newMap = new Map(this.localPlacements());
      newMap.set(cellId, selectedId);
      this.localPlacements.set(newMap);
      this.selectedTileId.set(null);
    }
  }

  resetLocalMove() {
    this.localPlacements.set(new Map());
    this.selectedTileId.set(null);
  }

  async submitMove() {
    this.lastError.set(null);

    const placements = Array.from(this.localPlacements().entries()).map(([cellId, tileId]) => ({
      cellId,
      tileId,
    }));

    if (placements.length === 0) return;

    const request: MoveRequestModel = { placements };

    try {
      const result: MoveResult = await this.gameHubService.handleMove(request);

      if (result.isSuccess) {
        this.resetLocalMove();
      } else {
        this.handleMoveError(result);
      }
    } catch (err) {
      this.lastError.set('Connection error occurred');
    }
  }

  private handleMoveError(result: MoveResult) {
    if (result.errorMessage) {
      this.lastError.set(result.errorCode!);
    } else if (result.errorCode) {
      this.lastError.set(`game.errors.${result.errorCode}`);
    } else {
      this.lastError.set('game.errors.UnknownError');
    }

    setTimeout(() => {
      this.lastError.set(null);
    }, 6000);
  }
}
