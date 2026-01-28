import { environment } from '../../../environments/environment';

export function getJwtToken(): string {
  return localStorage.getItem(environment.jwtStorageKey) ?? '';
}

export function getNickname(): string {
  return localStorage.getItem(environment.nicknameStorageKey) ?? '';
}

export function getSelectedLang(): string {
  const selectedLang = localStorage.getItem(environment.selectedLangStorageKey);
  return selectedLang === null || selectedLang === '' ? 'en' : selectedLang;
}

export function getPlayerId(): string {
  return localStorage.getItem(environment.playerIdStorageKey) ?? '';
}
