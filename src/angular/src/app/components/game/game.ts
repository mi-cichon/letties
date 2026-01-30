import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { GameDetails, LobbyPlayerDetails, MoveRequestModel, MoveResult } from '../../api';
import { GameHubService } from '../../services/game-hub-service';
import { LoadingSpinnerComponent } from '../../common/loading-spinner/loading-spinner';
import { TranslocoPipe } from '@jsverse/transloco';
import { PlayerNamePipe } from './pipes/player-name-pipe';
import { CellLabelPipe } from './pipes/cell-label-pipe';
import { getPlayerId } from '../../core/utils/token-utils';
import { SlicePipe } from '@angular/common';
import {
  CdkDrag,
  CdkDragDrop,
  CdkDragPlaceholder,
  CdkDropList,
  CdkDropListGroup,
} from '@angular/cdk/drag-drop';

@Component({
  selector: 'app-game',
  imports: [
    LoadingSpinnerComponent,
    TranslocoPipe,
    PlayerNamePipe,
    CellLabelPipe,
    SlicePipe,
    CdkDrag,
    CdkDropList,
    CdkDropListGroup,
    CdkDragPlaceholder,
  ],
  templateUrl: './game.html',
  styleUrl: './game.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Game {
  lobbyId = input.required<string>();
  players = input.required<LobbyPlayerDetails[]>();
  myId = signal(getPlayerId());

  private gameHubService = inject(GameHubService);

  gameState = this.gameHubService.gameState;

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

  tileOrder = signal<string[]>([]);

  orderedHand = computed(() => {
    const serverTiles = this.gameState()?.myHand?.tiles || [];
    const myOrder = this.tileOrder();

    // Jeśli nic jeszcze nie przesunąłeś, pokazuj tak, jak daje serwer
    if (myOrder.length === 0) {
      return serverTiles;
    }

    // Tworzymy mapę dla szybkiego wyszukiwania kafelka po ID
    const tileMap = new Map(serverTiles.map((t) => [t.tileId, t]));

    // Budujemy nową tablicę według TWOJEJ kolejności
    const result: any[] = [];

    // Najpierw dodaj te, które masz w swojej kolejności (jeśli wciąż są w ręce)
    myOrder.forEach((id) => {
      if (tileMap.has(id)) {
        result.push(tileMap.get(id));
        tileMap.delete(id); // Usuwamy z mapy, żeby nie powtórzyć
      }
    });

    // Na koniec dodaj kafelki, których nie było w Twoim tileOrder (np. nowo dobrane)
    tileMap.forEach((tile) => result.push(tile));

    return result;
  });

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

  onDropToBoard(event: CdkDragDrop<string>) {
    const cellId = event.container.data;
    const tileId = event.item.data;

    if (this.placedTilesMap().has(cellId)) return;

    const currentPlacements = new Map(this.localPlacements());

    for (const [key, value] of currentPlacements.entries()) {
      if (value === tileId) {
        currentPlacements.delete(key);
      }
    }

    currentPlacements.set(cellId, tileId);

    this.localPlacements.set(currentPlacements);

    this.selectedTileId.set(null);
  }

  onDropToRack(event: CdkDragDrop<any[]>) {
    // Jeśli to sortowanie na stojaku
    if (event.previousContainer === event.container) {
      // Pobieramy aktualny stan wizualny (to co gracz widzi przed upuszczeniem)
      const currentDisplay = this.orderedHand();
      const newOrder = currentDisplay.map((t) => t.tileId);

      // Przesuwamy element w tablicy ID
      const [movedId] = newOrder.splice(event.previousIndex, 1);
      newOrder.splice(event.currentIndex, 0, movedId);

      // Zapisujemy nową kolejność - to odpali computed!
      this.tileOrder.set(newOrder);

      console.log('Nowa kolejność ID:', newOrder);
    } else {
      // Tutaj logika usuwania z planszy (localPlacements.delete itd.)
      this.handleReturnToRack(event.item.data);
    }
  }

  private handleReturnToRack(tileId: string) {
    // 1. Czyścimy kafelek z planszy
    const currentPlacements = new Map(this.localPlacements());
    let wasOnBoard = false;

    for (const [cellId, id] of currentPlacements.entries()) {
      if (id === tileId) {
        currentPlacements.delete(cellId);
        wasOnBoard = true;
        break;
      }
    }

    if (wasOnBoard) {
      this.localPlacements.set(currentPlacements);

      // 2. Opcjonalnie: Dodaj ten tileId na początek Twojej kolejki na stojaku,
      // żeby nie "skakał" na koniec listy po powrocie z planszy
      const newOrder = [tileId, ...this.tileOrder().filter((id) => id !== tileId)];
      this.tileOrder.set(newOrder);
    }
  }

  canDropOnBoard = (drag: CdkDrag, drop: CdkDropList): boolean => {
    return this.isMyTurn();
  };

  moveItemInArray<T>(array: T[], fromIndex: number, toIndex: number): T[] {
    const newArray = [...array];

    const [movedItem] = newArray.splice(fromIndex, 1);

    newArray.splice(toIndex, 0, movedItem);

    return newArray;
  }
}
