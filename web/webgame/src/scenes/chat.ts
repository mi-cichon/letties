import Phaser from "phaser";
import { COLORS } from "../constants";

export interface ChatCapableScene extends Phaser.Scene {
  chatInput?: HTMLInputElement;
  sendChatMessage: (message: string) => void;
}

export interface ChatLayout {
  x: number;
  y: number;
  width: number;
  height: number;
}

export function createChatUI(scene: ChatCapableScene, layout: ChatLayout) {
  const { x: chatX, y: chatY, width: chatWidth, height: chatHeight } = layout;

  // Shadow
  scene.add
    .rectangle(
      chatX + chatWidth / 2 + 4,
      chatY + chatHeight / 2 + 4,
      chatWidth,
      chatHeight,
      0x000000,
    )
    .setAlpha(0.25)
    .setDepth(0);

  const chatContainer = scene.add
    .rectangle(
      chatX + chatWidth / 2,
      chatY + chatHeight / 2,
      chatWidth,
      chatHeight,
      COLORS.lightSand,
    )
    .setStrokeStyle(3, 0xffffff)
    .setDepth(1);

  scene.add
    .text(chatX + chatWidth / 2, chatY + 12, "Czat", {
      fontSize: "18px",
      fontFamily: "Arial",
      color: "#2d2416",
      fontStyle: "bold",
    })
    .setOrigin(0.5, 0)
    .setDepth(2);

  const chatDisplayDiv = document.createElement("div");
  chatDisplayDiv.style.position = "fixed";
  chatDisplayDiv.style.fontSize = "12px";
  chatDisplayDiv.style.fontFamily = "Arial";
  chatDisplayDiv.style.color = "#1a1410";
  chatDisplayDiv.style.backgroundColor = "transparent";
  chatDisplayDiv.style.overflowY = "auto";
  chatDisplayDiv.style.overflowX = "hidden";
  chatDisplayDiv.style.lineHeight = "1.4";
  chatDisplayDiv.style.whiteSpace = "pre-wrap";
  chatDisplayDiv.style.wordWrap = "break-word";
  chatDisplayDiv.style.padding = "4px 8px 12px 8px";
  chatDisplayDiv.style.margin = "0";
  chatDisplayDiv.style.pointerEvents = "auto";
  chatDisplayDiv.style.zIndex = "1000";
  chatDisplayDiv.style.boxSizing = "border-box";

  chatDisplayDiv.style.scrollbarWidth = "thin";
  chatDisplayDiv.style.scrollbarColor = "#8b7355 #d4c5b9";

  const styleSheet = document.createElement("style");
  styleSheet.textContent = `
    .chat-display::-webkit-scrollbar {
      width: 6px;
    }
    .chat-display::-webkit-scrollbar-track {
      background: #d4c5b9;
      border-radius: 3px;
    }
    .chat-display::-webkit-scrollbar-thumb {
      background: #8b7355;
      border-radius: 3px;
    }
    .chat-display::-webkit-scrollbar-thumb:hover {
      background: #705d49;
    }
  `;
  document.head.appendChild(styleSheet);
  chatDisplayDiv.classList.add("chat-display");

  const updateChatDisplayPosition = () => {
    const canvas = document.querySelector("canvas");
    if (canvas) {
      const rect = canvas.getBoundingClientRect();
      chatDisplayDiv.style.left = `${rect.left + chatX + 12}px`;
      chatDisplayDiv.style.top = `${rect.top + chatY + 36}px`;
      chatDisplayDiv.style.width = `${chatWidth - 16}px`;
      chatDisplayDiv.style.height = `${chatHeight - 82}px`;
    }
  };

  document.body.appendChild(chatDisplayDiv);
  updateChatDisplayPosition();
  window.addEventListener("resize", updateChatDisplayPosition);

  (scene as any).chatDisplayDiv = chatDisplayDiv;

  const inputElement = document.createElement("input");
  inputElement.type = "text";
  inputElement.placeholder = "Wpisz wiadomość...";
  inputElement.style.position = "fixed";
  inputElement.style.padding = "10px 8px";
  inputElement.style.fontSize = "14px";
  inputElement.style.backgroundColor = "#faf8f3";
  inputElement.style.color = "#1a1410";
  inputElement.style.border = "2px solid #d4c5b9";
  inputElement.style.borderRadius = "4px";
  inputElement.style.fontFamily = "Arial";
  inputElement.style.boxSizing = "border-box";
  inputElement.style.boxShadow = "0 2px 4px rgba(0, 0, 0, 0.1)";

  const buttonWidth = 48;
  const buttonGap = 8;

  const updateInputPosition = () => {
    const canvas = document.querySelector("canvas");
    inputElement.style.width = `${chatWidth - buttonWidth - buttonGap - 20}px`;

    if (canvas) {
      const rect = canvas.getBoundingClientRect();
      inputElement.style.left = `${rect.left + chatX + 10}px`;
      inputElement.style.top = `${rect.top + chatY + chatHeight - 46}px`;
    }
  };

  document.body.appendChild(inputElement);
  updateInputPosition();
  window.addEventListener("resize", updateInputPosition);

  const buttonX = chatX + chatWidth - buttonWidth / 2 - 8;
  const buttonY = chatY + chatHeight - 46 + 20;

  // Shadow for button
  scene.add
    .rectangle(buttonX + 2, buttonY + 2, buttonWidth, 36, 0x000000)
    .setAlpha(0.25)
    .setDepth(1);

  const sendButton = scene.add
    .rectangle(buttonX, buttonY, buttonWidth, 36, COLORS.accentBrown)
    .setStrokeStyle(2, 0xffffff)
    .setInteractive({ useHandCursor: true })
    .setDepth(2)
    .on("pointerover", () => {
      sendButton.setFillStyle(0xa0826d);
    })
    .on("pointerout", () => {
      sendButton.setFillStyle(COLORS.accentBrown);
    })
    .on("pointerdown", () => {
      const message = inputElement.value.trim();
      if (message) {
        scene.sendChatMessage(message);
        inputElement.value = "";
      }
    });

  scene.add
    .text(buttonX, buttonY, "→", {
      fontSize: "26px",
      color: "#ffffff",
      fontStyle: "bold",
    })
    .setOrigin(0.5)
    .setDepth(3);

  inputElement.addEventListener("keypress", (e: KeyboardEvent) => {
    if (e.key === "Enter") {
      const message = inputElement.value.trim();
      if (message) {
        scene.sendChatMessage(message);
        inputElement.value = "";
      }
    }
  });

  scene.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
    window.removeEventListener("resize", updateInputPosition);
    window.removeEventListener("resize", updateChatDisplayPosition);
    inputElement.remove();
    chatDisplayDiv.remove();
    sendButton.destroy();
  });

  scene.chatInput = inputElement;
}
