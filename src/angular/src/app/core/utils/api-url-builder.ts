import { environment } from '../../../environments/environment';

export function getLoginHubUrl(): string {
  return `${environment.apiUrl}${environment.loginHubUrl}`;
}

export function getGameHubUrl(): string {
  return `${environment.apiUrl}${environment.gameHubUrl}`;
}
