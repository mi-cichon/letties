import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslocoModule } from '@jsverse/transloco';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-privacy',
  standalone: true,
  imports: [CommonModule, TranslocoModule, RouterLink],
  templateUrl: './privacy.html',
  styleUrl: './privacy.scss'
})
export class Privacy {}
