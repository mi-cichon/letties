import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-post-game',
  imports: [],
  templateUrl: './post-game.html',
  styleUrl: './post-game.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PostGame { }
