using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RazorNova.Core.Enums;
using RazorNova.Core.Interfaces;
using RazorNova.Core.Models;

namespace RazorNova.App.ViewModels;

/// <summary>
/// Drives the Playlist tab: the list of saved playlists, and the
/// currently selected playlist's tracks. "Sort by Name / Date Modified"
/// here is display-only — it re-orders SelectedPlaylistTracks for
/// viewing but never rewrites PlaylistModel.TrackIds, which remains the
/// real playback order (used when shuffle is off).
/// </summary>
public sealed partial class PlaylistViewModel : ObservableObject
{
    private readonly IPlaylistRepository _playlistRepository;
    private readonly ITrackRepository _trackRepository;
    private readonly IPlaylistManager _playlistManager;

    private List<Track> _unsortedSelectedPlaylistTracks = new();

    public ObservableCollection<PlaylistModel> Playlists { get; } = new();
    public ObservableCollection<Track> SelectedPlaylistTracks { get; } = new();

    [ObservableProperty]
    private PlaylistModel? _selectedPlaylist;

    [ObservableProperty]
    private SortCriteria _selectedSortCriteria = SortCriteria.Name;

    // Bound TwoWay to the Rename TextBox. Seeded with the selected
    // playlist's current name whenever selection changes (see
    // OnSelectedPlaylistChanged below), so the box always starts out
    // showing something sensible instead of staying empty/stale.
    [ObservableProperty]
    private string _renameText = string.Empty;

    public PlaylistViewModel(
        IPlaylistRepository playlistRepository,
        ITrackRepository trackRepository,
        IPlaylistManager playlistManager)
    {
        _playlistRepository = playlistRepository;
        _trackRepository = trackRepository;
        _playlistManager = playlistManager;
    }

    /// <summary>Called once by the App composition root during startup to load all saved playlists.</summary>
    public async Task InitializeAsync()
    {
        // پاکسازی نام‌های خراب پیش از بارگذاری لیست
        await _playlistRepository.CleanCorruptedPlaylistNamesAsync();

        var playlists = await _playlistRepository.GetAllAsync();
        Playlists.Clear();
        foreach (var playlist in playlists)
            Playlists.Add(playlist);
    }

    [RelayCommand]
    private async Task NewPlaylistAsync()
    {
        var playlist = new PlaylistModel
        {
            Name = $"New Playlist {Playlists.Count + 1}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var saved = await _playlistRepository.AddAsync(playlist);
        Playlists.Add(saved);
        SelectedPlaylist = saved; // triggers OnSelectedPlaylistChanged, which seeds RenameText below
    }

    [RelayCommand]
    private async Task RenamePlaylistAsync()
    {
        if (SelectedPlaylist is null || string.IsNullOrWhiteSpace(RenameText)) return;

        SelectedPlaylist.Name = RenameText.Trim();
        await _playlistRepository.UpdateAsync(SelectedPlaylist);

        // PlaylistModel is a plain Core model (no INotifyPropertyChanged),
        // so the bound list won't notice the Name change by itself.
        // Removing and re-inserting the same reference forces the UI to
        // re-render that item with its new name.
        var index = Playlists.IndexOf(SelectedPlaylist);
        if (index < 0) return;

        var playlist = Playlists[index];
        Playlists.RemoveAt(index);
        Playlists.Insert(index, playlist);
        SelectedPlaylist = playlist;
    }

    [RelayCommand]
    private async Task DeletePlaylistAsync()
    {
        if (SelectedPlaylist is null) return;

        await _playlistRepository.DeleteAsync(SelectedPlaylist.Id);
        Playlists.Remove(SelectedPlaylist);
        SelectedPlaylist = null;
    }

    [RelayCommand]
    private async Task AddTrackToSelectedPlaylistAsync(Track track)
    {
        if (SelectedPlaylist is null) return;
        if (SelectedPlaylist.TrackIds.Contains(track.Id)) return; // already in this playlist — no duplicates

        SelectedPlaylist.TrackIds.Add(track.Id);
        await _playlistRepository.UpdateAsync(SelectedPlaylist);
        await LoadSelectedPlaylistTracksAsync();
    }

    [RelayCommand]
    private async Task RemoveTrackFromSelectedPlaylistAsync(Track track)
    {
        if (SelectedPlaylist is null) return;

        SelectedPlaylist.TrackIds.Remove(track.Id);
        await _playlistRepository.UpdateAsync(SelectedPlaylist);
        await LoadSelectedPlaylistTracksAsync();
    }

    [RelayCommand]
    private void PlaySelectedPlaylist()
    {
        if (SelectedPlaylistTracks.Count == 0) return;
        _playlistManager.SetQueue(SelectedPlaylistTracks.ToList(), 0);
    }

    [RelayCommand]
    private void PlayTrackInPlaylist(Track track)
    {
        var index = SelectedPlaylistTracks.IndexOf(track);
        if (index < 0) return;
        _playlistManager.SetQueue(SelectedPlaylistTracks.ToList(), index);
    }

    partial void OnSelectedPlaylistChanged(PlaylistModel? value)
    {
        RenameText = value?.Name ?? string.Empty;
        _ = LoadSelectedPlaylistTracksAsync();
    }

    partial void OnSelectedSortCriteriaChanged(SortCriteria value) => ApplyDisplaySort();

    private async Task LoadSelectedPlaylistTracksAsync()
    {
        if (SelectedPlaylist is null)
        {
            _unsortedSelectedPlaylistTracks = new();
            SelectedPlaylistTracks.Clear();
            return;
        }

        var resolved = await _trackRepository.GetByIdsAsync(SelectedPlaylist.TrackIds);
        _unsortedSelectedPlaylistTracks = resolved.ToList();
        ApplyDisplaySort();
    }

    private void ApplyDisplaySort()
    {
        IEnumerable<Track> sorted = SelectedSortCriteria switch
        {
            SortCriteria.Name => _unsortedSelectedPlaylistTracks.OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase),
            SortCriteria.ModifiedDate => _unsortedSelectedPlaylistTracks.OrderByDescending(t => t.FileModifiedAtUtc),
            _ => _unsortedSelectedPlaylistTracks
        };

        SelectedPlaylistTracks.Clear();
        foreach (var track in sorted)
            SelectedPlaylistTracks.Add(track);
    }
}