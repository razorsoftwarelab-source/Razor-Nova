# Razor Nova 🎵

A free, modular music player for Windows with a neon-lit dark/light theme, built with WPF, .NET 8, and a clean MVVM architecture.

---

## ✨ Features

- **Playback** — MP3, FLAC, WAV, M4A support via NAudio
- **Library** — Scan folders or add individual files; search, sort, and manage your music collection
- **Playlists** — Create, rename, delete, and manage track queues (shuffle, repeat, mute)
- **Dual Theme** — Midnight Purple (night) and White + Violet (day) with neon glow effects
- **Mini Player** — Compact, always-on-top playback window
- **System Tray** — Minimize to tray, control playback, and receive notifications
- **Media Keys** — Hardware media key support
- **Fully Modular** — 10 independent projects communicating only through interfaces

---

## 📁 Full Directory Structure
RazorNova/
├── RazorNova.sln
│
├── src/
│   ├── RazorNova.Core/                          [net8.0]
│   │   ├── RazorNova.Core.csproj
│   │   ├── Enums/
│   │   │   ├── PlaybackStatus.cs
│   │   │   ├── RepeatMode.cs
│   │   │   ├── ThemeMode.cs
│   │   │   └── SortCriteria.cs
│   │   ├── Models/
│   │   │   ├── Track.cs
│   │   │   ├── PlaylistModel.cs
│   │   │   ├── AppSettings.cs
│   │   │   ├── ScanResult.cs
│   │   │   ├── AddFilesResult.cs
│   │   │   └── AlbumArtResult.cs
│   │   └── Interfaces/
│   │       ├── IAudioPlayer.cs
│   │       ├── ILibraryService.cs
│   │       ├── IPlaylistManager.cs
│   │       ├── IThemeManager.cs
│   │       ├── ILocalizationService.cs
│   │       ├── IMediaKeyListener.cs
│   │       ├── ITrayService.cs
│   │       ├── IAlbumArtService.cs
│   │       ├── IMetadataReader.cs
│   │       ├── ITrackRepository.cs
│   │       ├── IPlaylistRepository.cs
│   │       └── ISettingsService.cs
│   │
│   ├── RazorNova.Playback/                      [net8.0 + NAudio 2.3.0]
│   │   ├── RazorNova.Playback.csproj
│   │   └── AudioPlayerService.cs
│   │
│   ├── RazorNova.Metadata/                      [net8.0-windows + TagLibSharp 2.3.0]
│   │   ├── RazorNova.Metadata.csproj
│   │   ├── MetadataReaderService.cs
│   │   └── AlbumArtCacheService.cs
│   │
│   ├── RazorNova.Data/                          [net8.0 + Microsoft.Data.Sqlite 8.0.21]
│   │   ├── RazorNova.Data.csproj
│   │   ├── DatabaseContext.cs
│   │   ├── TrackRepository.cs
│   │   ├── PlaylistRepository.cs
│   │   └── SettingsRepository.cs
│   │
│   ├── RazorNova.Library/                       [net8.0, Core only]
│   │   ├── RazorNova.Library.csproj
│   │   └── FolderScannerService.cs
│   │
│   ├── RazorNova.Playlist/                      [net8.0, Core only]
│   │   ├── RazorNova.Playlist.csproj
│   │   └── PlaylistManagerService.cs
│   │
│   ├── RazorNova.Theme/                         [net8.0-windows + Microsoft.Win32.SystemEvents 10.0.2]
│   │   ├── RazorNova.Theme.csproj
│   │   └── ThemeManagerService.cs
│   │
│   ├── RazorNova.Localization/                  [net8.0]
│   │   ├── RazorNova.Localization.csproj
│   │   ├── Resources/
│   │   │   ├── Strings.resx                     (English, default)
│   │   │   └── Strings.fa.resx                  (Persian)
│   │   └── LocalizationService.cs
│   │
│   ├── RazorNova.Platform/                      [net8.0-windows + UseWPF + H.NotifyIcon.Wpf 2.4.1]
│   │   ├── RazorNova.Platform.csproj
│   │   ├── MediaKeyListener.cs
│   │   └── TrayIconService.cs
│   │
│   └── RazorNova.App/                           [net8.0-windows + UseWPF + CommunityToolkit.Mvvm 8.4.2 + MS.Extensions.DI/Logging 8.0.1]
│       ├── RazorNova.App.csproj
│       ├── App.xaml
│       ├── App.xaml.cs                          (composition root)
│       ├── ViewModels/
│       │   ├── PlayerControlsViewModel.cs       (singleton, shared with MiniPlayer)
│       │   ├── LibraryViewModel.cs
│       │   ├── PlaylistViewModel.cs
│       │   └── MainViewModel.cs
│       ├── Themes/
│       │   ├── NightTheme.xaml                  (Midnight Purple neon)
│       │   └── DayTheme.xaml                    (White + Violet)
│       ├── Styles/
│       │   └── ControlStyles.xaml
│       ├── Converters/
│       │   ├── ByteArrayToImageSourceConverter.cs
│       │   ├── EnumToBooleanConverter.cs
│       │   ├── TimeSpanToShortStringConverter.cs
│       │   └── InverseBooleanConverter.cs
│       └── Views/
│           ├── MainWindow.xaml
│           ├── MainWindow.xaml.cs
│           ├── MiniPlayerWindow.xaml
│           ├── MiniPlayerWindow.xaml.cs
│           ├── AboutDialog.xaml
│           └── AboutDialog.xaml.cs
│
└── tests/                                       (planned for future)
├── RazorNova.Playback.Tests/
├── RazorNova.Library.Tests/
└── RazorNova.Playlist.Tests/

```

---

## 🧱 Architecture

Every project depends only on `RazorNova.Core` interfaces.  
No direct references between services — everything is wired in `App.xaml.cs` via Microsoft Dependency Injection.

| Project | Responsibility |
|---------|----------------|
| **Core** | Interfaces, Models, Enums |
| **Playback** | Audio engine (NAudio) |
| **Metadata** | Tag & cover reading (TagLib#) |
| **Data** | SQLite repositories |
| **Library** | Folder scanning & import logic |
| **Playlist** | Playlist & queue management |
| **Theme** | Theme switching (Night/Day/System) |
| **Localization** | Localization services (.resx) |
| **Platform** | Tray icon & media keys |
| **App** | WPF UI + Composition Root |

---

## ⚙️ Tech Stack

- **.NET 8** / C# 12
- **WPF** with MVVM (CommunityToolkit.Mvvm)
- **NAudio 2.3** for audio output
- **TagLibSharp 2.3** for metadata
- **Microsoft.Data.Sqlite 8.0** for persistence
- **H.NotifyIcon.Wpf 2.4** for tray functionality

---

## 🚀 Getting Started

### Prerequisites
- .NET 8 SDK
- Windows 10/11

### Build & Run
```bash
git clone https://github.com/RazorSoftwareLab/RazorNova.git
cd RazorNova
dotnet build RazorNova.sln
dotnet run --project src/RazorNova.App
```

Adding Music

1. Click Scan Folder... to import a directory (and subfolders).
2. Or click Add Files... to select individual audio files.

---

🧪 Current Status

· Build: ✅ 10 projects, 0 errors
· Core functionality: ✅ Fully operational
· Known issues: A runtime exception may appear on startup (under investigation); minor visual polish (custom scrollbar) pending.

---

📄 License

This project is licensed under the BSD 3-Clause License.
See the LICENSE file for the full license text.

Copyright (c) 2026, Razor Software Lab.

---

Made with 🐺💜 by Razor Software Lab.
