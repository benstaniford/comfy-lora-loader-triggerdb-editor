# LoRA DB Editor

A WPF application for managing LoRA safetensor files and their trigger database for ComfyUI.

## Features

- **Dark-themed UI** - Modern dark interface for comfortable viewing
- **Fuzzy Search** - Quickly find LoRA files by typing partial names
- **Tree Navigation** - Browse LoRA files in a hierarchical folder structure
- **File ID Validation** - Automatically validates file IDs against actual file contents
- **File ID Updates** - One-click update for missing or incorrect file IDs
- **Real-time Warnings** - Visual indicators for missing files or ID mismatches

## Requirements

- Windows OS
- .NET 8.0 SDK
- Visual Studio 2022 (recommended)

## Building the Application

1. Open `LoraDbEditor.sln` in Visual Studio 2022
2. Build the solution (Ctrl+Shift+B)
3. Run the application (F5)

Alternatively, build from command line:
```bash
dotnet build LoraDbEditor.sln
dotnet run --project LoraDbEditor/LoraDbEditor.csproj
```

## File Locations

The application expects files in these locations:

- **Database**: `%USERPROFILE%\Documents\ComfyUI\user\default\user-db\lora-triggers.json`
- **LoRA Files**: `%USERPROFILE%\Documents\ComfyUI\models\loras\`

## Usage

### Searching for Files

1. Type in the search box at the top
2. The combo box will fuzzy-match file paths as you type
3. Select a file from the dropdown to view its details

### Tree Navigation

1. Expand folders in the left panel
2. Click on any `.safetensors` file to view its details

### Viewing and Editing Details

Once a file is selected, the right panel shows editable fields:

- **File Path** - The relative path used as the database key
- **File ID** - SHA1 hash identifier for the file
- **Active Triggers** - Currently active trigger words (editable, supports multiline)
- **All Triggers** - Complete list of trigger words with descriptions (editable, supports multiline)
- **Source URL** - Optional URL where the LoRA was downloaded from (supports drag and drop from browser)
- **Suggested Strength** - Optional recommended strength value
- **Notes** - Optional notes about the LoRA (editable, supports multiline)
- **Gallery** - Image gallery showing example outputs (drag and drop images to add, click to view full size)

All editable fields automatically save changes when you click "Save Database".

### Managing Gallery Images

The Gallery section displays 256x256 thumbnails of example images:
- **View Images**: Click any thumbnail to open it in your default image viewer
- **Add Images**: Drag and drop image files onto the "Add Image" box
- **Supported Formats**: JPG, PNG, BMP, GIF, WEBP
- **Storage**: Images are stored in `%USERPROFILE%\Documents\ComfyUI\user\default\user-db\lora-triggers-pictures\`

### File ID Validation

The application automatically validates file IDs:

- **Red Warning** - File not found or ID mismatch
- **Orange Warning** - File ID missing or set to "unknown"
- **No Warning** - File ID is correct

Click "Update File ID" to fix missing or incorrect IDs.

### Saving Changes

1. After updating file IDs, the "Save Database" button becomes enabled
2. Click "Save Database" to write changes to `lora-triggers.json`
3. The application will prompt you to save if you try to close with unsaved changes

## File ID Algorithm

File IDs are calculated using SHA1 hash of:
1. File size (as string)
2. First 1MB of file
3. Last 1MB of file (if file > 1MB)

This provides fast hashing of large files while maintaining uniqueness.

## Architecture

### Models
- **LoraEntry** - Represents a LoRA file entry with triggers and file ID
- **TreeViewNode** - Hierarchical node for tree view display

### Services
- **FileIdCalculator** - Computes SHA1 file IDs
- **LoraDatabase** - Manages JSON database read/write operations
- **FileSystemScanner** - Scans filesystem and implements fuzzy search

### UI
- **MainWindow** - Main application window with search, tree, and details panels
- **App** - Application entry point with dark theme resources

## JSON Format

The database uses JSON with newlines encoded as `\n`:

```json
{
  "path/to/lora": {
    "active_triggers": "trigger1\ntrigger2",
    "all_triggers": "trigger1, trigger2\nRecommended Strength: 1.0",
    "file_id": "abc123...",
    "source_url": "https://civitai.com/models/12345",
    "suggested_strength": "0.8-1.2",
    "notes": "Works well with landscapes\nBest at 1024x1024",
    "gallery": [
      "path_to_lora_20231201120000.png",
      "path_to_lora_20231201120030.jpg"
    ]
  }
}
```

**Notes:**
- The path keys do NOT include the `.safetensors` extension
- `source_url`, `suggested_strength`, `notes`, and `gallery` are optional fields
- Newlines in `active_triggers`, `all_triggers`, and `notes` are encoded as `\n`
- Gallery contains filenames (not full paths) of images stored in the pictures folder
