# DnDMasterAI

DnDMasterAI is a C# console-based Dungeons & Dragons adventure game powered by AI. The game uses a local Large Language Model (LLM) via Ollama to act as a Dungeon Master, dynamically generating story content and responding to player actions in real-time.

## Features

* **AI-Driven Narrative**: Uses `llama3` via Ollama to provide a cohesive and reactive storytelling experience in Dutch.
* **Character Management**: Create, load, and delete characters with persistent storage in JSON format.
* **Combat System**: A turn-based combat engine that calculates hits, misses, and damage based on character and enemy stats like Strength and Dexterity.
* **Procedural Encounters**: Automatically generates enemies and loot based on the player's current power level.
* **Session Persistence**: Saves your story progress and inventory so you can resume your adventure later.
* **Inventory System**: Collect items during your journey with support for different rarity levels and weights.

## Project Structure

The project is organized into several key components:

* **Models**: Defines the data structures for `Character`, `Enemy`, `InventoryItem`, and `CombatResult`.
* **Services**:
    * `AIService`: Manages communication with the Ollama process.
    * `CombatService`: Handles the mechanics of battle.
    * `EncounterService`: Generates random enemies and loot.
    * **`GameEngine`**: The core loop that manages story flow, player input, and AI prompting.
    * `LoginService`: Manages the initial menus for session and character loading.

## Requirements

* **.NET 8.0 SDK**
* **Ollama**: Must be installed and running locally with the `llama3` model pulled.

## Getting Started

1.  **Clone the repository**:
    ```bash
    git clone <repository-url>
    ```
2.  **Restore and Build**:
    ```bash
    dotnet restore
    dotnet build
    ```
3.  **Run the application**:
    ```bash
    dotnet run --project DnDMasterAI
    ```

## How to Play

1.  **Start/Load**: Use the main menu to create a new character or load an existing one.
2.  **The Story**: The AI Dungeon Master will describe a scene. Type your actions in plain text (e.g., "Open de kist" or "Val de goblin aan").
3.  **Inventory**: Type `inventory` or `inv` at any time to see your items.
4.  **Picking up items**: Use commands like `pick up [item]`, `neem [item]`, or `pak [item]` to add things to your bag.
5.  **Saving**: Type `exit` to save your current story and character state.

## Configuration

* **Line Endings**: The project uses `.gitattributes` to automatically normalize line endings.
* **Ignored Files**: Standard Visual Studio and .NET build artifacts are excluded via `.gitignore`.
 ---------------------------
 This project was largely vibecoded.
