import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  OnDestroy,
  signal,
} from '@angular/core';
import {
  GameDetails,
  LobbyPlayerDetails,
  MoveRequestModel,
  MoveResult,
  PlacedTileDetails,
  TileDefinitionDetails,
} from '../../api';
import { GameHubService } from '../../services/game-hub-service';
import { LoadingSpinnerComponent } from '../../common/loading-spinner/loading-spinner';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
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
import { LobbyNamePipe } from '../overview/pipes/lobby-name.pipe';
import { GetPlayerTimePipe } from './pipes/get-player-time-pipe';
import { GetPlayerOfflinePipe } from './pipes/get-player-offline-pipe';
import { GetBotInfoPipe } from '../lobby/pipes/get-bot-info-pipe';
import { BoardPositionPipe } from './pipes/board-position-pipe';

@Component({
  selector: 'app-game',
  imports: [
    LoadingSpinnerComponent,
    TranslocoPipe,
    CellLabelPipe,
    CdkDrag,
    CdkDropList,
    CdkDropListGroup,
    CdkDragPlaceholder,
    LobbyNamePipe,
    GetPlayerTimePipe,
    GetPlayerOfflinePipe,
    GetBotInfoPipe,
    BoardPositionPipe,
  ],
  templateUrl: './game.html',
  styleUrl: './game.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Game implements OnDestroy {
  lobbyId = input.required<string>();
  players = input.required<LobbyPlayerDetails[]>();
  myId = signal(getPlayerId());

  private gameHubService = inject(GameHubService);
  private translocoService = inject(TranslocoService);
  private turnNotificationSound = new Audio('/assets/sounds/turn-start.mp3');
  private lastNotifiedTurnStartedAt = '';

  gameState = this.gameHubService.gameState;
  lobbyState = this.gameHubService.lobbyState.asReadonly();

  placedTilesMap = computed(() => {
    const map = new Map<string, PlacedTileDetails>();
    this.gameState()?.boardContent?.placedTiles?.forEach((tile) => {
      map.set(tile.cellId!, tile);
    });
    return map;
  });

  tileDefsMap = computed(() => {
    const map = new Map<string, TileDefinitionDetails>();
    this.gameState()?.tileDefinitions?.forEach((def) => {
      map.set(def.valueId!, def);
    });
    return map;
  });

  localSelectedValues = signal<Map<string, string | null>>(new Map());
  activeBlankPickerCell = signal<string | null>(null);
  blankPickerPos = signal<{ left: number; top: number } | null>(null);

  blankOptions = computed(() => {
    const defs = Array.from(this.tileDefsMap().values());
    return defs
      .filter((d: any) => d?.valueText && d.valueText !== '?')
      .map((d: any) => ({ text: d.valueText, valueId: d.valueId }));
  });

  lastError = signal<string | null>(null);

  selectedTileId = signal<string | null>(null);
  localPlacements = signal<Map<string, string>>(new Map());

  usedTileIds = computed(() => new Set(this.localPlacements().values()));

  scoresByDepleted = computed(() => {
    const scores = this.gameState()?.scores ?? [];

    return [...scores].sort((a, b) => {
      return Number(a.timeDepleted) - Number(b.timeDepleted);
    });
  });

  isMyTurn = computed(() => this.gameState()?.currentTurnPlayerId === this.myId());

  amIPlaying = computed(() => {
    return this.gameState()?.scores?.find((x) => x.playerId === this.myId()) !== undefined;
  });

  playerRemainingTimes = signal<Map<string, string>>(new Map());

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

  selectedTilesForSwap = signal<Set<string>>(new Set());
  isSwapMode = signal<boolean>(false);

  constructor() {
    effect(() => {
      const state = this.gameState();
      const myId = this.myId();
      const turnStart = state?.currentTurnStartedAt || '';

      if (turnStart !== this.lastNotifiedTurnStartedAt) {
        this.resetLocalMove();
      }

      if (state?.currentTurnPlayerId === myId && turnStart !== this.lastNotifiedTurnStartedAt) {
        this.lastNotifiedTurnStartedAt = turnStart;
        this.playTurnSound();
        this.showTurnNotification();
      }
    });
  }

  private playTurnSound() {
    try {
      this.turnNotificationSound.volume = 0.15;
      this.turnNotificationSound.currentTime = 0;
      this.turnNotificationSound.play();
    } catch (e) {}
  }

  private showTurnNotification() {
    if ('Notification' in window && Notification.permission === 'granted' && document.hidden) {
      const title = this.translocoService.translate('lobby.notification.turnTitle');
      const body = this.translocoService.translate('lobby.notification.turnBody');

      new Notification(title, {
        body: body,
        icon: '/assets/icon.svg',
      });
    }
  }

  getTileValueIdFromHand(tileId: string | null): string | undefined {
    if (!tileId) return undefined;
    return this.gameState()?.myHand?.tiles?.find((t) => t.tileId === tileId)?.valueId;
  }

  async ngOnInit() {
    await this.gameHubService.getGameDetails();
    this.requestNotificationPermission();
  }

  private requestNotificationPermission() {
    if ('Notification' in window && Notification.permission === 'default') {
      Notification.requestPermission();
    }
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

      const vId = this.getTileValueIdFromHand(selectedId);
      const def = this.tileDefsMap().get(vId!);
      if (def?.valueText === '?') {
        const selMap = new Map(this.localSelectedValues());
        selMap.set(cellId, null);
        this.localSelectedValues.set(selMap);
        this.openBlankPicker(cellId);
      }
    }
  }

  resetLocalMove() {
    this.selectedTilesForSwap.set(new Set<string>());
    this.isSwapMode.set(false);
    this.localPlacements.set(new Map());
    this.localSelectedValues.set(new Map());
    this.activeBlankPickerCell.set(null);
    this.selectedTileId.set(null);
  }

  async submitMove() {
    this.lastError.set(null);

    const placements = Array.from(this.localPlacements().entries()).map(([cellId, tileId]) => {
      const sel = this.localSelectedValues().get(cellId);
      return sel ? { cellId, tileId, selectedValueId: sel } : { cellId, tileId };
    });

    if (placements.length === 0) return;

    this.selectedTilesForSwap.set(new Set<string>());
    this.isSwapMode.set(false);

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

    const vId = this.getTileValueIdFromHand(tileId);
    const def = this.tileDefsMap().get(vId!);
    if (def?.valueText === '?') {
      const selMap = new Map(this.localSelectedValues());
      selMap.set(cellId, null);
      this.localSelectedValues.set(selMap);
      this.openBlankPicker(cellId);
    }
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
    let removedCell: string | null = null;

    for (const [cellId, id] of currentPlacements.entries()) {
      if (id === tileId) {
        currentPlacements.delete(cellId);
        wasOnBoard = true;
        removedCell = cellId;
        break;
      }
    }

    if (wasOnBoard) {
      this.localPlacements.set(currentPlacements);

      if (removedCell) {
        const selMap = new Map(this.localSelectedValues());
        selMap.delete(removedCell);
        this.localSelectedValues.set(selMap);
        if (this.activeBlankPickerCell() === removedCell) this.activeBlankPickerCell.set(null);
      }

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

  toggleSwapMode() {
    if (this.localPlacements().size > 0) {
      return;
    }
    this.isSwapMode.update((v) => !v);
    this.selectedTilesForSwap.set(new Set());
  }

  toggleTileSelection(tileId: string) {
    if (!this.isSwapMode()) return;

    this.selectedTilesForSwap.update((set) => {
      const newSet = new Set(set);
      if (newSet.has(tileId)) newSet.delete(tileId);
      else newSet.add(tileId);
      return newSet;
    });
  }

  async confirmSwap() {
    const tileIds = Array.from(this.selectedTilesForSwap());
    if (tileIds.length === 0) return;

    await this.gameHubService.swapTiles(tileIds);

    this.selectedTilesForSwap.set(new Set<string>());
    this.isSwapMode.set(false);
  }

  async passTurn() {
    console.log('skip');
    if (!this.isMyTurn()) {
      return;
    }

    this.selectedTilesForSwap.set(new Set<string>());
    this.isSwapMode.set(false);

    await this.gameHubService.skipTurn();
  }

  chooseBlankValue(cellId: string, valueId: string) {
    const selMap = new Map(this.localSelectedValues());
    selMap.set(cellId, valueId);
    this.localSelectedValues.set(selMap);
    if (this.activeBlankPickerCell() === cellId) this.activeBlankPickerCell.set(null);
  }

  clearBlankSelection(cellId: string) {
    const selMap = new Map(this.localSelectedValues());
    selMap.delete(cellId);
    this.localSelectedValues.set(selMap);
    if (this.activeBlankPickerCell() === cellId) this.activeBlankPickerCell.set(null);
  }

  openBlankPicker(cellId: string) {
    console.log('open picker for cell:', cellId);
    this.activeBlankPickerCell.set(cellId);
  }

  private remainingSeconds: Map<string, number> = new Map();
  private countdownTimer: number | null = null;

  private parseMMSS(value: string | undefined | null): number {
    if (!value) return 0;
    const parts = value.split(':').map((p) => parseInt(p, 10));
    if (parts.some((n) => isNaN(n))) return 0;
    let seconds = 0;
    let multiplier = 1;
    for (let i = parts.length - 1; i >= 0; i--) {
      seconds += parts[i] * multiplier;
      multiplier *= 60;
    }
    return seconds;
  }

  private formatSecondsToMMSS(secs: number): string {
    const m = Math.floor(secs / 60);
    const s = secs % 60;
    return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  }

  private initializeTimersFromState() {
    const state = this.gameState();
    const scores = state?.scores ?? [];
    const now = Date.now();

    const map = new Map<string, number>();
    for (const s of scores) {
      const pid = s.playerId!;
      const base = this.parseMMSS(s.timeRemaining ?? undefined);
      map.set(pid, base);
    }

    const current = state?.currentTurnPlayerId ?? null;
    const startedAt = state?.currentTurnStartedAt ? Date.parse(state.currentTurnStartedAt) : null;
    if (current && startedAt) {
      const elapsed = Math.floor((now - startedAt) / 1000);
      const prev = map.get(current) ?? 0;
      map.set(current, Math.max(0, prev - elapsed));
    }

    this.remainingSeconds = map;
    this.emitRemainingStrings();
    this.startTickForCurrent();
  }

  private emitRemainingStrings() {
    const out = new Map<string, string>();
    for (const [pid, secs] of this.remainingSeconds.entries()) {
      out.set(pid, this.formatSecondsToMMSS(Math.max(0, Math.floor(secs))));
    }
    this.playerRemainingTimes.set(out);
  }

  private startTickForCurrent() {
    if (this.countdownTimer) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }

    const current = this.gameState()?.currentTurnPlayerId ?? null;
    if (!current) return;

    this.countdownTimer = window.setInterval(() => {
      const prev = this.remainingSeconds.get(current) ?? 0;
      const next = Math.max(0, prev - 1);
      this.remainingSeconds.set(current, next);
      this.emitRemainingStrings();
    }, 1000);
  }

  private readonly _timersEffect = effect(() => {
    this.gameState();
    this.initializeTimersFromState();
  });

  ngOnDestroy(): void {
    if (this.countdownTimer) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  }
}
