import * as signalR from "@microsoft/signalr";
import { HUB_URLS, JwtStorageKey, PlayerNameStorageKey } from "../constants";

export const loginToGame = async (
  nick: string,
): Promise<{ token: string; success: boolean }> => {
  const loginConnection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URLS.login)
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();

  try {
    await loginConnection.start();
    const token = await loginConnection.invoke<string>("Login", nick);
    await loginConnection.stop();

    localStorage.setItem(JwtStorageKey, token);
    localStorage.setItem(PlayerNameStorageKey, nick);

    return { token, success: true };
  } catch (error) {
    console.error("Login failed:", error);
    await loginConnection.stop();
    return { token: "", success: false };
  }
};

export const validateToken = async (): Promise<boolean> => {
  const token = localStorage.getItem(JwtStorageKey);
  if (!token) return false;

  const loginConnection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URLS.login)
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();

  try {
    await loginConnection.start();
    const isValid = await loginConnection.invoke<boolean>("Validate", token);
    await loginConnection.stop();
    return isValid;
  } catch (error) {
    console.error("Token validation failed:", error);
    await loginConnection.stop();
    return false;
  }
};

export const clearAuth = () => {
  localStorage.removeItem(JwtStorageKey);
  localStorage.removeItem(PlayerNameStorageKey);
};
