<p align="center">
  <h1 align="center">
    <img src="./Design/Logo.png" alt="ClipCull logo" height="100">
  </h1>

  <p align="center">
    Footage management for FPV pilots — from SD card to social media
    <br/>
    <br/>
    <a href="https://github.com/flowhl/ClipCull/releases">Download</a> •
    <a href="https://github.com/flowhl/ClipCull/issues">Report bug</a> •
    <a href="https://github.com/flowhl/ClipCull/issues">Request feature</a>
  </p>
  <p align="center">
    <a href="https://github.com/flowhl/ClipCull/releases">
      <img src="https://img.shields.io/github/downloads/flowhl/ClipCull/total" alt="Downloads">
    </a>
    <a href="https://github.com/flowhl/ClipCull/issues/">
      <img src="https://img.shields.io/github/issues/flowhl/ClipCull" alt="Issues">
    </a>
    <a href="https://github.com/flowhl/ClipCull/blob/main/LICENSE">
      <img src="https://img.shields.io/github/license/flowhl/ClipCull" alt="License">
    </a>
  </p>
</p>

## About the project

ClipCull is a footage management tool built for FPV pilots who are tired of digging through SD cards full of raw footage. It sits between your drone and your social media — browse your footage folders, mark the good parts, tag and rate everything, then render stabilized clips ready to post, without ever opening a full editing suite.

If you fly FPV and end up with dozens of multi-gigabyte files after every session that you never get around to editing, this is for you.

## Features

### Video preview & playback
- LibVLC-based video player with smooth playback of large files
- Rotation support (0°, 90°, 180°, 270°) for cameras mounted at odd angles
- Frame stepping (forward/backward) for finding the exact moment
- Timeline scrubbing with a draggable playhead
- Volume control with persistent settings

### Clip marking
- Set **In/Out points** to define a clip range on any video
- Create multiple **sub-clips** within a single video, each with its own name and color
- Place **markers** at specific timestamps as bookmarks or notes
- Visual timeline displays all clip regions, markers and the current playhead at a glance

### Tagging & organization
- **Color-coded tags** — define your own tags with custom colors (e.g. forest, mountains, freestyle, cinematic)
- **Rating** from 1 to 5 stars per clip
- **Pick / Reject** status for quick sorting
- **User metadata** fields: title, description, author, location, reel, shot, camera
- **Clip filtering** by tags (AND/OR logic), rating, pick status or free text search
- **Clip browser** with thumbnail previews, type indicators (main clip vs sub-clip) and multi-selection

### Gyroflow stabilization & rendering
- Built-in **Gyroflow integration** — processes clips through the Gyroflow CLI for video stabilization
- **Render queue** for batch processing multiple clips
- Auto-discovers your Gyroflow installation, or accepts a custom path
- Configurable render options: rotation, audio toggle, alternative audio codec (PCM s16le)
- GPU-accelerated rendering
- Progress tracking per clip

### Metadata storage
- All metadata is stored in **XML sidecar files** alongside your video files
- Human-readable format — you can open and edit them in any text editor
- Portable — metadata travels with your footage when you move files
- Includes clip points, sub-clips, markers, tags, ratings, user metadata and rotation

### UI & workflow
- **Dockable panel layout** — rearrange panels however you like and save your layout
- **Customizable hotkeys** for all common actions
- **Auto-save** for sidecar metadata (configurable)
- **Dark theme** throughout
- Remembers your last folder, volume and window layout between sessions
- Technical metadata display: codec, resolution, FPS, bitrate, duration

## Modules

ClipCull is organized into four tabs:

| Tab | Purpose |
|-----|---------|
| **Editing** | Main workspace — video preview, timeline controls, file browser, properties and metadata panels |
| **Clips** | Browse and filter all clips in a folder with thumbnails, preview and controls |
| **Render Queue** | Queue and process clips through Gyroflow for stabilized renders |
| **Settings** | General settings, tag management and hotkey configuration |

## Default hotkeys

| Action | Key |
|--------|-----|
| Play / Pause | `Space` |
| Next frame | `Right` |
| Previous frame | `Left` |
| Step forward (small) | `Shift+Right` |
| Step backward (small) | `Shift+Left` |
| Set In point | `I` |
| Set Out point | `O` |
| Start sub-clip | `Q` |
| End sub-clip | `E` |
| Add marker | `M` |
| Pick | `P` |
| Reject | `X` |
| Remove pick/reject | `U` |
| Save | `Ctrl+S` |
| Open | `Ctrl+O` |
| Reload | `Ctrl+R` / `F5` |
| Undo | `Ctrl+Z` |
| Redo | `Ctrl+Y` |

All hotkeys can be remapped in Settings.

## Sidecar file format

ClipCull stores all metadata in an XML file next to each video (e.g. `GX010123.xml` for `GX010123.MP4`). Example:

```xml
<SidecarContent>
  <InPoint>
    <Timestamp>1500</Timestamp>
    <Type>InPoint</Type>
  </InPoint>
  <OutPoint>
    <Timestamp>10000</Timestamp>
    <Type>OutPoint</Type>
  </OutPoint>
  <Markers>
    <Marker>
      <Timestamp>5000</Timestamp>
      <Title>Nice dive</Title>
    </Marker>
  </Markers>
  <SubClips>
    <SubClip>
      <StartTime>1500</StartTime>
      <EndTime>4200</EndTime>
      <Title>Tree gap</Title>
      <Color><A>255</A><R>99</R><G>132</G><B>255</B></Color>
    </SubClip>
  </SubClips>
  <UserMetadata>
    <Title>Morning session</Title>
    <Location>Forest trail</Location>
    <Camera>GoPro HERO 11</Camera>
    <Rating>4</Rating>
    <Pick>true</Pick>
    <Rotation>0</Rotation>
    <Tags>
      <Tag><Name>forest</Name><Color>#228B22</Color></Tag>
      <Tag><Name>highlight</Name><Color>#FFD700</Color></Tag>
    </Tags>
  </UserMetadata>
</SidecarContent>
```

Timestamps are in milliseconds. You never need to touch these files manually, but you can if you want to.

## Supported formats

MP4, MOV, AVI, MKV, WMV, FLV, WebM, M4V

## Requirements

- **Windows** (WPF / .NET 8)
- [Gyroflow](https://gyroflow.xyz/) (optional — only needed for stabilization rendering)
- FFmpeg (auto-downloaded on first run)

## Installation

Download the latest release from the [Releases](https://github.com/flowhl/ClipCull/releases) page and run the installer.

## Built with

- [.NET 8 / WPF](https://dotnet.microsoft.com/) — application framework
- [LibVLCSharp](https://github.com/videolan/libvlcsharp) — video playback
- [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) — video metadata extraction
- [CliWrap](https://github.com/Tyrrrz/CliWrap) — Gyroflow CLI integration
- [AvalonDock](https://github.com/Dirkster99/AvalonDock) — dockable panel layout
- [Material Design In XAML](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) — UI theme
- [Velopack](https://github.com/velopack/velopack) — auto-updates

## Contributing

Contributions are welcome. If you have suggestions or want to report a bug, feel free to [open an issue](https://github.com/flowhl/ClipCull/issues/new). If you want to implement something, fork the repo, make your changes and open a pull request.

## License

Distributed under the Apache 2.0 License. See [LICENSE](https://github.com/flowhl/ClipCull/blob/main/LICENSE) for more information.
