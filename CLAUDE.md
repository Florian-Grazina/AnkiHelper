# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build AnkiHelper.sln                          # add -c Release for release
dotnet run --project AnkiHelper/AnkiHelper.csproj
```

Windows-only WPF app (`net9.0-windows`). `TreatWarningsAsErrors` and `Nullable` are enabled — every warning breaks the build. There is no test project; verification means building and running against a live Anki.

**Runtime requirement:** Anki installed with the AnkiConnect add-on listening on port 8765. Most features fail (error banner) if Anki isn't reachable.

## Architecture

Single-window MVVM app using CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]` → bound in XAML as `XxxCommand`). No DI container: `MainWindow` news up `MainWindowViewModel`, which news up `AnkiConnectService`. `MainWindow.Window_Loaded` calls `OnLoad()`.

`MainWindow.xaml` holds all styles inline, for an authentic Windows 98 look: `WindowStyle=None` + `WindowChrome` with a hand-drawn frame and title bar (caption buttons and size grip use Marlett glyphs; their handlers and a DWM call disabling Windows 11 rounded corners live in `MainWindow.xaml.cs`), Win98 palette brushes as resources, Microsoft Sans Serif 11px aliased text. `App.xaml` resources are empty. The `TextBox` template shows the control's `Tag` as a gray placeholder while `Text` is empty.

Every `[RelayCommand]` uses `CanExecute = nameof(CanRunCommand)` (`!IsBusy`), and `IsBusy` notifies all of them: keep this on new commands so they can't run concurrently (e.g. a deck refresh during an import).

### Anki communication (`Services/AnkiConnectService.cs`)
- All calls are JSON POSTs to AnkiConnect (API `version = 6`) via a static `HttpClient`.
- New actions should go through the private `CallAsync<T>(payload)` helper, which serializes an anonymous object with `System.Text.Json` and deserializes the `result` property. Note it ignores AnkiConnect's `error` field, and `AddCardAsync` returns the raw response string unchecked — failed adds (e.g. duplicates, since `allowDuplicate = false`) are silent.
- `EnsureAnkiIsRunning()` starts `anki.exe` from Program Files / Program Files (x86) / `%LOCALAPPDATA%\Programs\Anki` if no `anki` process exists.

### Card import flow
1. The user pastes a JSON array of `VocabDTO` into the text box (the placeholder documents the expected format).
2. `AddCards` deserializes it to `VocabDTO` with **Newtonsoft.Json** (the AnkiConnect side uses System.Text.Json — don't mix them up), then adds each item with a 25 ms delay.
3. Each card is a `"Basic (and reversed card)"` note: `Front = English`, `Back = Japanese`, tagged with `Lesson`. `WordType` is parsed but not sent to Anki.
4. After a successful import, decks are reloaded and the text box is cleared.

### Deck model
Decks are organized as `Parent::L-<n>` sub-decks. `GetDecksAsync` collapses these to the parent name (generated regex `^(.*?)::L-\d+`), so the dropdown only lists parents and cards are added to the parent deck. Deck-wide operations query `deck:"<name>"`, which includes sub-decks.

### Maintenance commands
- `AddCss` (bound in UI): overwrites the CSS of every note type used in the deck — this affects those note types globally, not just the deck.
- `FixTags` (not bound in UI): on notes with exactly two numeric tags where the higher is ≥ 5, replaces them with a merged `"high-low"` tag.
- `DeleteAdjTag` (not bound in UI): body is commented out; currently a no-op.
