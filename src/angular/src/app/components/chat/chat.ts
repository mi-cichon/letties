import { Component, input, output, viewChild, ElementRef, effect } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-chat',
  standalone: true,
  templateUrl: './chat.html',
  styleUrl: './chat.scss',
  imports: [TranslocoPipe],
})
export class Chat {
  messages = input.required<any[]>();
  onSendMessage = output<string>();

  scrollContainer = viewChild<ElementRef>('scrollContainer');

  constructor() {
    effect(() => {
      const msgs = this.messages();
      const container = this.scrollContainer();
      if (container) {
        setTimeout(() => {
          container.nativeElement.scrollTop = container.nativeElement.scrollHeight;
        }, 50);
      }
    });
  }

  sendMessage(input: HTMLInputElement) {
    const val = input.value.trim();
    if (val) {
      this.onSendMessage.emit(val);
      input.value = '';
    }
  }
}
