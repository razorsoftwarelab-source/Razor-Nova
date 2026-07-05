using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RazorNova.Core.Enums;
using RazorNova.Core.Interfaces;
using RazorNova.Core.Models;

namespace RazorNova.App.ViewModels;

/// <summary>
/// Drives the Library tab: the searchable/sortable track list, the "Scan
/// Folder" flow, and the "Add Files" flow. Exposes raw scan progress data
/// (counts, a TimeSpan) rather than pre-formatted strings — the View is
/// responsible for formatting/localizing those numbers, keeping this
/// ViewModel language-agnostic. Double-clicking a track hands the currently
/// displayed list to IPlaylistManager as the new playback queue.
/// </summary>
public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly ITrackRepository _trackRepository;
    private readonly ILibraryService _libraryService;
    private readonly IPlaylistManager _playlistManager;
    private readonly ISettingsService _settingsService;

    private CancellationTokenSource? _searchDebounceCts;
    private CancellationTokenSource? _scanCts;
    private string? _lastScannedFolderPath;

    public ObservableCollection<Track> Tracks { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private SortCriteria _selectedSortCriteria = SortCriteria.Name;

    [ObservableProperty]
    private bool _isLoading;

    // Despite the name, this now covers ANY library-import operation in
    // progress — folder scan OR manual add-files — not just scanning.
    // Kept the original name rather than renaming it, since MainWindow.xaml
    // already binds to it and renaming would ripple into that file too.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveFolderCommand))]   // جدید
    [NotifyCanExecuteChangedFor(nameof(DeleteTrackCommand))]    // جدید
    private bool _isScanning;

    // Drives the overlay's headline text — "Scanning..." vs "Adding
    // files..." — since IsScanning alone can't distinguish which operation
    // is running. Set at the start of each command, read by MainWindow.xaml.
    [ObservableProperty]
    private string _importStatusText = "Scanning...";

    [ObservableProperty]
    private int _scanFilesFoundCount;

    [ObservableProperty]
    private TimeSpan _scanTotalDuration;

    [ObservableProperty]
    private int _scanFailedFilesCount;

    public LibraryViewModel(
        ITrackRepository trackRepository,
        ILibraryService libraryService,
        IPlaylistManager playlistManager,
        ISettingsService settingsService)
    {
        _trackRepository = trackRepository;
        _libraryService = libraryService;
        _playlistManager = playlistManager;
        _settingsService = settingsService;
    }

    /// <summary>Called once by the App composition root during startup to load the saved last-scanned folder and the initial track list.</summary>
    public async Task InitializeAsync()
    {
        var settings = await _settingsService.LoadAsync();
        _lastScannedFolderPath = settings.LastScannedFolderPath;
        await ReloadTracksAsync(CancellationToken.None);
    }

    [RelayCommand]
    private void PlayTrack(Track track)
    {
        var index = Tracks.IndexOf(track);
        if (index < 0) return;
        _playlistManager.SetQueue(Tracks.ToList(), index);
    }

    [RelayCommand(CanExecute = nameof(CanScanFolder))]
    private async Task ScanFolderAsync()
    {
        // Native WPF folder picker (Microsoft.Win32.OpenFolderDialog,
        // available since .NET 8) — a thin, one-call UI concern, kept
        // directly here rather than behind a dedicated Core interface
        // since there is no real logic to abstract.
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder to scan",
            InitialDirectory = _lastScannedFolderPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
        };

        if (dialog.ShowDialog() != true) return;

        var folderPath = dialog.FolderName;
        IsScanning = true;
        ImportStatusText = "Scanning...";
        ScanFilesFoundCount = 0;
        ScanTotalDuration = TimeSpan.Zero;
        ScanFailedFilesCount = 0;
        _scanCts = new CancellationTokenSource();

        // System.Progress<T> captures the UI thread's SynchronizationContext
        // at construction time, so Report() calls below — even though
        // ILibraryService runs with ConfigureAwait(false) internally —
        // automatically marshal back here. No manual Dispatcher needed.
        var progress = new Progress<(int FilesFoundSoFar, TimeSpan TotalDurationSoFar)>(p =>
        {
            ScanFilesFoundCount = p.FilesFoundSoFar;
            ScanTotalDuration = p.TotalDurationSoFar;
        });

        try
        {
            var result = await _libraryService.ScanFolderAsync(folderPath, progress, _scanCts.Token);
            ScanFailedFilesCount = result.FailedFilesCount;

            _lastScannedFolderPath = folderPath;
            await PersistLastFolderAsync(folderPath);
        }
        catch (OperationCanceledException)
        {
            // The scan is all-or-nothing (see FolderScannerService): a
            // cancelled scan persists nothing at all, so the library is
            // left exactly as it was before this scan started.
        }
        finally
        {
            IsScanning = false;
            _scanCts = null;
            await ReloadTracksAsync(CancellationToken.None);
        }
    }

    [RelayCommand(CanExecute = nameof(CanScanFolder))]
    private async Task AddFilesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select audio files to add",
            Multiselect = true,
            Filter = "Audio Files (*.mp3;*.flac;*.wav;*.m4a)|*.mp3;*.flac;*.wav;*.m4a|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        IsScanning = true;
        ImportStatusText = "Adding files...";
        ScanFilesFoundCount = 0;
        ScanTotalDuration = TimeSpan.Zero;
        ScanFailedFilesCount = 0;
        _scanCts = new CancellationTokenSource();

        var progress = new Progress<(int FilesFoundSoFar, TimeSpan TotalDurationSoFar)>(p =>
        {
            ScanFilesFoundCount = p.FilesFoundSoFar;
            ScanTotalDuration = p.TotalDurationSoFar;
        });

        try
        {
            var result = await _libraryService.AddFilesAsync(dialog.FileNames, progress, _scanCts.Token);
            ScanFailedFilesCount = result.FailedFilesCount;
        }
        catch (OperationCanceledException)
        {
            // All-or-nothing, same as ScanFolderAsync — see FolderScannerService.
        }
        finally
        {
            IsScanning = false;
            _scanCts = null;
            await ReloadTracksAsync(CancellationToken.None);
        }
    }

    // ========== NEW: Remove Folder ==========
    [RelayCommand(CanExecute = nameof(CanScanFolder))]
    private async Task RemoveFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder to remove from library",
            InitialDirectory = _lastScannedFolderPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
        };

        if (dialog.ShowDialog() != true) return;

        var folderPath = dialog.FolderName;
        IsScanning = true;
        _scanCts = new CancellationTokenSource();

        try
        {
            await _trackRepository.DeleteByFolderAsync(folderPath, _scanCts.Token);
            if (string.Equals(_lastScannedFolderPath, folderPath, StringComparison.OrdinalIgnoreCase))
            {
                _lastScannedFolderPath = null;
                await PersistLastFolderAsync(string.Empty);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsScanning = false;
            _scanCts = null;
            await ReloadTracksAsync(CancellationToken.None);
        }
    }

    // ========== NEW: Delete Single Track ==========
    [RelayCommand(CanExecute = nameof(CanScanFolder))]
    private async Task DeleteTrackAsync(Track track)
    {
        if (track is null || track.Id <= 0) return;

        IsScanning = true;
        _scanCts = new CancellationTokenSource();

        try
        {
            await _trackRepository.DeleteAsync(track.Id, _scanCts.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsScanning = false;
            _scanCts = null;
            await ReloadTracksAsync(CancellationToken.None);
        }
    }

    private bool CanScanFolder() => !IsScanning;

    // Cancels whichever operation is currently running — ScanFolderAsync or
    // AddFilesAsync both use the same _scanCts field, so one Cancel command
    // covers both without needing a second one.
    [RelayCommand]
    private void CancelScan() => _scanCts?.Cancel();

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;
        _ = DebouncedReloadAsync(cts.Token);
    }

    partial void OnSelectedSortCriteriaChanged(SortCriteria value) =>
        _ = ReloadTracksAsync(CancellationToken.None);

    private async Task DebouncedReloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Short debounce so a fast typist doesn't fire one SQL query
            // per keystroke — only the last keystroke in a burst actually searches.
            await Task.Delay(250, cancellationToken);
            await ReloadTracksAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer keystroke — expected, not an error.
        }
    }

    private async Task ReloadTracksAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        try
        {
            var results = string.IsNullOrWhiteSpace(SearchText)
                ? await _trackRepository.GetAllAsync(SelectedSortCriteria, cancellationToken)
                : await _trackRepository.SearchAsync(SearchText, SelectedSortCriteria, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            Tracks.Clear();
            foreach (var track in results)
                Tracks.Add(track);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer search/sort change — expected, not an error.
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PersistLastFolderAsync(string folderPath)
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            settings.LastScannedFolderPath = folderPath;
            await _settingsService.SaveAsync(settings);
        }
        catch
        {
            // Best-effort — failing to remember the last folder must never crash the app.
        }
    }
}