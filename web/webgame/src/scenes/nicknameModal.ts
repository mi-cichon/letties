export function createNicknameModal(onSubmit: (nick: string) => void) {
  // Tworzymy kontener modala
  const modal = document.createElement("div");
  modal.id = "nickname-modal";
  modal.style.cssText = `
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background: rgba(0, 0, 0, 0.7);
    display: flex;
    justify-content: center;
    align-items: center;
    z-index: 1000;
  `;

  // Zawartość modala
  const content = document.createElement("div");
  content.style.cssText = `
    background: #8b7355;
    border: 3px solid #5d4e37;
    border-radius: 10px;
    padding: 40px;
    text-align: center;
    min-width: 350px;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
  `;

  // Tytuł
  const title = document.createElement("h2");
  title.textContent = "SCRABBLE";
  title.style.cssText = `
    color: #ffffff;
    margin: 0 0 20px 0;
    font-family: Arial, sans-serif;
    font-size: 32px;
    font-weight: bold;
  `;
  content.appendChild(title);

  // Podtytuł
  const subtitle = document.createElement("p");
  subtitle.textContent = "Podaj nick, aby rozpocząć";
  subtitle.style.cssText = `
    color: #fffef5;
    margin: 0 0 25px 0;
    font-family: Arial, sans-serif;
    font-size: 16px;
  `;
  content.appendChild(subtitle);

  // Input
  const input = document.createElement("input");
  input.type = "text";
  input.placeholder = "Twój nick...";
  input.style.cssText = `
    width: 100%;
    padding: 12px;
    margin-bottom: 20px;
    border: 2px solid #5d4e37;
    border-radius: 5px;
    background: #d4c5b9;
    color: #000000;
    font-size: 16px;
    font-family: Arial, sans-serif;
    box-sizing: border-box;
    transition: border-color 0.3s;
  `;
  input.addEventListener("focus", () => {
    input.style.borderColor = "#d4c5b9";
  });
  input.addEventListener("blur", () => {
    input.style.borderColor = "#5d4e37";
  });
  content.appendChild(input);

  // Przycisk
  const button = document.createElement("button");
  button.textContent = "Zagraj";
  button.style.cssText = `
    width: 100%;
    padding: 12px;
    background: #5d4e37;
    color: #ffffff;
    border: none;
    border-radius: 5px;
    font-size: 16px;
    font-family: Arial, sans-serif;
    font-weight: bold;
    cursor: pointer;
    transition: background 0.3s;
  `;
  button.addEventListener("mouseover", () => {
    button.style.background = "#6d5e47";
  });
  button.addEventListener("mouseout", () => {
    button.style.background = "#5d4e37";
  });
  button.addEventListener("click", handleSubmit);
  content.appendChild(button);

  // Error message
  const errorMsg = document.createElement("p");
  errorMsg.style.cssText = `
    color: #ff6b6b;
    margin: 15px 0 0 0;
    font-family: Arial, sans-serif;
    font-size: 14px;
    display: none;
  `;
  content.appendChild(errorMsg);

  modal.appendChild(content);
  document.body.appendChild(modal);

  function handleSubmit() {
    const nick = input.value.trim();
    if (!nick) {
      errorMsg.textContent = "Nick nie może być pusty!";
      errorMsg.style.display = "block";
      input.focus();
      return;
    }

    modal.remove();
    onSubmit(nick);
  }

  // Enter w input wysyła formularz
  input.addEventListener("keypress", (e: KeyboardEvent) => {
    if (e.key === "Enter") {
      handleSubmit();
    }
  });

  // Auto-focus
  input.focus();
}
