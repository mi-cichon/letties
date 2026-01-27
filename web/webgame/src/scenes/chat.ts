import Phaser from "phaser";
import { COLORS } from "../constants";

export interface ChatCapableScene extends Phaser.Scene {
  chatDisplay?: Phaser.GameObjects.Text;
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

  scene.chatDisplay = scene.add
    .text(chatX + 12, chatY + 40, "", {
      fontSize: "12px",
      fontFamily: "Arial",
      color: "#1a1410",
      wordWrap: { width: chatWidth - 24 },
      lineSpacing: 5,
    })
    .setOrigin(0, 0)
    .setDepth(2);

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
    inputElement.remove();
    sendButton.destroy();
    scene.chatDisplay?.destroy();
  });

  scene.chatInput = inputElement;
}
