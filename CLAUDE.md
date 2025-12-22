# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LoRA DB Editor is a WPF desktop application for managing ComfyUI LoRA safetensor files and their trigger word database. The app provides a visual interface for browsing LoRA files, validating file IDs, and updating the `lora-triggers.json` database.

## Building and Running

```bash
# Build the solution
dotnet build LoraDbEditor.sln

# Run the application
dotnet run --project LoraDbEditor/LoraDbEditor.csproj

# Build for release
dotnet build LoraDbEditor.sln -c Release
```

**Requirements**: .NET 8.0 SDK, Windows OS

## File System Conventions

The application operates on a fixed directory structure in the user's profile:

- **JSON Database**: `%USERPROFILE%\Documents\ComfyUI\user\default\user-db\lora-triggers.json`
- **LoRA Files**: `%USERPROFILE%\Documents\ComfyUI\models\loras\`

These paths are hardcoded in `LoraDatabase.cs` constructor and cannot be changed at runtime.

## JSON Database Format

The `lora-triggers.json` file uses a specific structure:

- **Keys**: Relative file paths WITHOUT the `.safetensors` extension (e.g., `"chroma/80sFantasyMovieChroma"`)
- **Newlines**: Encoded as `\n` in strings (e.g., `"trigger1\nRecommended Strength: 1.0"`)
- **file_id**: Optional field, may be absent, `null`, or `"unknown"` for legacy entries

```json
{
  "path/to/lora": {
    "active_triggers": "trigger1, trigger2",
    "all_triggers": "trigger1, trigger2\nRecommended Strength: 1.0",
    "file_id": "abc123..."
  }
}
```

When deserializing, the dictionary key becomes the `Path` property, and the full filesystem path is constructed by appending `.safetensors`.

## File ID Algorithm

The file ID is a SHA1 hash computed from:
1. File size (as UTF-8 string)
2. First 1MB of file content
3. Last 1MB of file content (if file > 1MB)

This algorithm matches the Python implementation in `~/dot-files/scripts/get-file-id`. The C# implementation is in `FileIdCalculator.cs` and must remain compatible with the Python version.

## Architecture

### Data Flow

1. **Startup** (`MainWindow.Window_Loaded`):
   - `LoraDatabase.LoadAsync()` reads and deserializes JSON
   - `FileSystemScanner.ScanForLoraFiles()` discovers all `.safetensors` files
   - For each database entry, file ID is validated by calculating actual ID and comparing
   - Tree view is built from file paths, search combo box is populated

2. **File Selection** (via search or tree):
   - `LoadLoraEntry()` retrieves or creates `LoraEntry`
   - File ID validation status determines UI warnings (red/orange/none)
   - Details panel displays triggers and file ID information

3. **File ID Update**:
   - User clicks "Update File ID" button
   - `LoraDatabase.UpdateFileId()` modifies in-memory entry
   - "Save Database" button becomes enabled
   - `LoraDatabase.SaveAsync()` writes JSON back to disk

### Key Components

- **LoraEntry Model**: Hybrid model with JSON-serialized properties (`active_triggers`, `all_triggers`, `file_id`) and runtime properties marked `[JsonIgnore]` (`Path`, `FullPath`, `FileExists`, `FileIdValid`, `CalculatedFileId`)

- **FileSystemScanner**: Implements fuzzy search by scoring paths based on character match patterns (exact match > starts with > contains > ordered characters)

- **LoraDatabase**: Manages in-memory dictionary where keys are relative paths (without extension) and values are `LoraEntry` objects. On load, populates runtime properties; on save, serializes only JSON properties.

### UI Components

- **Left Panel**: TreeView built hierarchically from forward-slash-separated paths
- **Right Panel**:
  - Editable ComboBox with fuzzy search
  - File path title
  - File ID section with validation warnings
  - Active/All triggers text boxes (All triggers converts `\n` to actual newlines for display)

## Theme System

Dark theme is defined globally in `App.xaml` with static resources:
- `BackgroundBrush` (#1E1E1E)
- `SurfaceBrush` (#2D2D30)
- `BorderBrush` (#3F3F46)
- `TextBrush` (#E0E0E0)
- `AccentBrush` (#007ACC)
- `ErrorBrush` (#F44747) - used for file ID mismatches
- `SuccessBrush` (#4EC9B0) - used for correct file IDs

When adding new UI controls, use these resource keys to maintain visual consistency.

## Important Implementation Details

- **Path Separators**: Always use forward slashes (`/`) in database keys, even though Windows uses backslashes. `FileSystemScanner` converts backslashes to forward slashes after calling `Path.GetRelativePath()`.

- **Tree View Construction**: The tree is built recursively by splitting paths on `/` and creating `TreeViewNode` objects with `Children` collections. Folders are sorted before files.

- **Fuzzy Search**: Implemented in `FileSystemScanner.FuzzySearch()` with scoring system. Updates happen via `TextChanged` event on the ComboBox's internal `PART_EditableTextBox`.

- **Unsaved Changes Tracking**: `_hasUnsavedChanges` flag is set when file IDs are updated, enables Save button, and triggers confirmation dialog on window close.
