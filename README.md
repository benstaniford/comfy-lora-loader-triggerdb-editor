# LoRA DB Editor

A desktop application for managing your ComfyUI LoRA collection and trigger word database. Designed to work seamlessly with the [LoRA Loader with TriggerDB](https://github.com/benstaniford/comfy-lora-loader-with-triggerdb) custom node.

## What It Does

LoRA DB Editor helps you organize and manage your LoRA files by:

- 📝 Managing trigger words and activation phrases for each LoRA
- 🖼️ Creating visual galleries of example images for each LoRA
- 🔗 Tracking where you downloaded each LoRA from
- 📊 Maintaining recommended strength settings
- 🔍 Quickly finding LoRAs with fuzzy search
- ✅ Validating file integrity with automatic ID checking

This tool makes it easy to remember which trigger words to use with each LoRA and see visual examples of what they produce.

## Installation

### Requirements

- Windows PC
- [ComfyUI](https://github.com/comfyanonymous/ComfyUI) with the [LoRA Loader with TriggerDB](https://github.com/benstaniford/comfy-lora-loader-with-triggerdb) node installed

### Download & Install

1. Download the latest Windows installer from the [Releases page](https://github.com/benstaniford/comfy-lora-loader-triggerdb-editor/releases)
2. Run the installer
3. Launch "LoRA DB Editor" from your Start Menu

The installer includes everything you need - no additional downloads required!

## Setup

The application automatically looks for your ComfyUI files in these locations:

- **LoRA Files**: `%USERPROFILE%\Documents\ComfyUI\models\loras\`
- **Database**: `%USERPROFILE%\Documents\ComfyUI\user\default\user-db\lora-triggers.json`

If you're using a custom ComfyUI installation path, you'll need to alter these paths in the settings dialog.

## How to Use

### Finding Your LoRAs

There are two ways to browse your collection:

**Quick Search** (Top of window)
- Type any part of a LoRA name
- The search uses "fuzzy matching" - you don't need to type the exact name
- Example: typing "fan" will find "80sFantasyMovieChroma"
- Select from the dropdown to open that LoRA

**Folder Tree** (Left panel)
- Browse your LoRAs organized by folder
- Expand folders to see what's inside
- Click any `.safetensors` file to view its details

### Managing a LoRA

When you select a LoRA, the right panel shows all its information:

#### Description
A brief description of what the LoRA does or its style.

#### Active Triggers
The main trigger words you need to use in your prompts. These are the words shown in the ComfyUI node dropdown.

#### All Triggers
The complete list of trigger words, including variations and recommended settings. This is your reference guide.

#### Gallery
Visual examples of what the LoRA produces:
- **Add Images**: Drag and drop images from anywhere (including from ComfyUI output folders!)
- **View Images**: Click any thumbnail to open it full-size
- **Supported Formats**: JPG, PNG, BMP, GIF, WEBP

#### Source URL
Where you downloaded the LoRA from (Civitai, HuggingFace, etc.)
- Type the URL or drag and drop it from your browser address bar

#### Suggested Strength
The recommended strength value (e.g., "0.8-1.2" or "1.0")

#### Notes
Any additional information about using the LoRA - compatibility notes, tips, special instructions, etc.

### Saving Your Changes

1. Edit any fields you want to update
2. The "Save Database" button will become active
3. Click "Save Database" to save all changes
4. Your changes will automatically appear in the ComfyUI node

### File ID Warnings

The app and comfy can keep track of your loras if they are moved/renamed by their file-id (A partial hash of the file).  It also validates that your LoRA files haven't been corrupted or modified:

- ✅ **No Warning** - File is valid
- 🟧 **Orange Warning** - File ID hasn't been calculated yet (click "Update File ID")
- 🟥 **Red Warning** - File is missing or has been modified (may need to re-download)

File IDs help ensure you're using the exact same LoRA file that your trigger words were designed for.

## Tips & Tricks

- **Multiline Fields**: Active Triggers, All Triggers, and Notes support multiple lines - press Enter to add line breaks
- **Browser Drag & Drop**: Drag URLs directly from your browser's address bar into the Source URL field
- **CivitAI download**: Drag lora download links directly from CivitAI.com directly into folders in the navigation panel
- **Bulk Updates**: You can update multiple LoRAs in one session - just switch between them and save once at the end
- **Git integration**: If you manage your database folder with git, the app will detect this and allow you to commit changes for both the JSON file and the images.
- **Backup**: Your database is just a JSON file - back it up regularly!
- **Image Organization**: The app copies images to a central folder, so you can delete the originals after adding them

## Workflow Integration

This editor works hand-in-hand with the [LoRA Loader with TriggerDB](https://github.com/benstaniford/comfy-lora-loader-with-triggerdb) ComfyUI node:

1. **Download** a new LoRA and save it to your `models/loras/` folder
2. **Open** LoRA DB Editor and find your new LoRA
3. **Add** trigger words from the LoRA's documentation
4. **Create** a gallery by generating test images in ComfyUI and dragging them into the editor
5. **Save** your changes
6. **Refresh** the TriggerDB node in ComfyUI to see your new LoRA with all its triggers!

## Troubleshooting

**"File not found" warnings for files that exist**
- Make sure your LoRAs are in `%USERPROFILE%\Documents\ComfyUI\models\loras\`
- Check that the file has the `.safetensors` extension

**Changes don't appear in ComfyUI**
- Make sure you clicked "Save Database" in the editor
- Refresh or reload your workflow in ComfyUI

**Images won't add to gallery**
- Check the file format (JPG, PNG, BMP, GIF, WEBP supported)
- Make sure you're dragging onto the "Add Image" box

**App won't start**
- Install the [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Check that you're running Windows

## Support & Contributions

- **Issues**: Report bugs or request features in the [Issues](../../issues) section
- **Contributions**: Pull requests are welcome!
- **ComfyUI Node**: For issues with the ComfyUI integration, visit the [LoRA Loader with TriggerDB](https://github.com/benstaniford/comfy-lora-loader-with-triggerdb) repository

## License

See [LICENSE](LICENSE) file for details.
