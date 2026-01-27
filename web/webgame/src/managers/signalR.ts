import * as signalR from "@microsoft/signalr";
import { HUB_URLS, JwtStorageKey } from "../constants";
import { BackendEvents } from "../models/types";

let connection: signalR.HubConnection | null = null;

export const createConnection = async (): Promise<signalR.HubConnection> => {
  if (connection && connection.state === signalR.HubConnectionState.Connected) {
    return connection;
  }

  connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URLS.game, {
      accessTokenFactory: () => localStorage.getItem(JwtStorageKey) || "",
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();

  await connection.start();
  return connection;
};

export const getConnection = (): signalR.HubConnection | null => {
  return connection;
};

export const setupGameHandlers = (handlers: Partial<BackendEvents>) => {
  if (!connection) {
    console.warn("Connection not initialized. Call createConnection() first.");
    return;
  }

  Object.entries(handlers).forEach(([eventName, handler]) => {
    if (handler) {
      connection!.on(eventName, handler);
    }
  });
};

export const removeGameHandlers = (eventNames: (keyof BackendEvents)[]) => {
  if (!connection) return;

  eventNames.forEach((eventName) => {
    connection!.off(eventName);
  });
};

export const closeConnection = async () => {
  if (connection) {
    await connection.stop();
    connection = null;
  }
};
