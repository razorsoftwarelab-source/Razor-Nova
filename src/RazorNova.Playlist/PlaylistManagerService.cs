using RazorNova.Core.Enums;
using RazorNova.Core.Interfaces;
using RazorNova.Core.Models;

namespace RazorNova.Playlist;

/// <summary>
/// In-memory implementation of IPlaylistManager. Maintains the current
/// position in two parallel ways: a plain sequential index into Queue
/// (used directly when shuffle is off) and a separately tracked shuffle
/// order — a random permutation of queue indices, used only when shuffle
/// is on. Toggling shuffle never changes CurrentTrack itself; it only
/// changes how MoveNext/MovePrevious compute what comes next.
/// </summary>
public sealed class PlaylistManagerService : IPlaylistManager
{
    private readonly Random _random = new();

    private List<Track> _queue = new();
    private int _currentIndex = -1;

    private List<int>? _shuffleOrder;
    private int _shufflePosition = -1;

    private bool _isShuffleEnabled;

    public IReadOnlyList<Track> Queue => _queue;

    public Track? CurrentTrack =>
        _currentIndex >= 0 && _currentIndex < _queue.Count ? _queue[_currentIndex] : null;

    public int CurrentIndex => _currentIndex;

    public bool IsShuffleEnabled
    {
        get => _isShuffleEnabled;
        set
        {
            if (_isShuffleEnabled == value) return;
            _isShuffleEnabled = value;

            if (value)
                RegenerateShuffleOrder();
            else
            {
                _shuffleOrder = null;
                _shufflePosition = -1;
            }
            // CurrentTrack never changes just from toggling shuffle, so no event here.
        }
    }

    public RepeatMode Repeat { get; set; } = RepeatMode.Off;

    public event EventHandler<Track?>? CurrentTrackChanged;

    public void SetQueue(IReadOnlyList<Track> tracks, int startIndex = 0)
    {
        _queue = tracks.ToList();
        _currentIndex = _queue.Count == 0 ? -1 : Math.Clamp(startIndex, 0, _queue.Count - 1);

        if (_isShuffleEnabled)
            RegenerateShuffleOrder();
        else
        {
            _shuffleOrder = null;
            _shufflePosition = -1;
        }

        RaiseCurrentTrackChanged();
    }

    public Track? MoveNext()
    {
        if (_queue.Count == 0) return SetIndexAndReturn(-1);
        return SetIndexAndReturn(ComputeAdjacentIndex(forward: true));
    }

    public Track? MovePrevious()
    {
        if (_queue.Count == 0) return SetIndexAndReturn(-1);
        return SetIndexAndReturn(ComputeAdjacentIndex(forward: false));
    }

    public Track? HandleTrackEnded()
    {
        if (_queue.Count == 0) return null;

        if (Repeat == RepeatMode.One)
        {
            // Same track again — index is unchanged, so deliberately do
            // NOT raise CurrentTrackChanged (nothing actually changed).
            // The caller uses this return value directly to restart playback.
            return CurrentTrack;
        }

        if (Repeat == RepeatMode.Off && IsAtEndOfPlayOrder())
        {
            // Last track finished naturally with no repeat — playback
            // stops. CurrentTrack/CurrentIndex are deliberately left as-is.
            return null;
        }

        // RepeatMode.All, or RepeatMode.Off but not yet at the last track:
        // behaves exactly like an automatic "Next".
        return MoveNext();
    }

    private bool IsAtEndOfPlayOrder()
    {
        if (_isShuffleEnabled && _shuffleOrder is not null)
            return _shufflePosition >= _shuffleOrder.Count - 1;

        return _currentIndex >= _queue.Count - 1;
    }

    private int ComputeAdjacentIndex(bool forward)
    {
        if (_isShuffleEnabled)
        {
            if (_shuffleOrder is null || _shuffleOrder.Count != _queue.Count)
                RegenerateShuffleOrder();

            _shufflePosition += forward ? 1 : -1;

            if (_shufflePosition >= _shuffleOrder!.Count)
            {
                // Exhausted the shuffle order — reshuffle for a fresh
                // random sequence instead of repeating the same one.
                RegenerateShuffleOrder();
                _shufflePosition = 0;
            }
            else if (_shufflePosition < 0)
            {
                RegenerateShuffleOrder();
                _shufflePosition = _shuffleOrder!.Count - 1;
            }

            return _shuffleOrder![_shufflePosition];
        }

        var delta = forward ? 1 : -1;
        return ((_currentIndex + delta) % _queue.Count + _queue.Count) % _queue.Count;
    }

    private Track? SetIndexAndReturn(int newIndex)
    {
        if (_currentIndex != newIndex)
        {
            _currentIndex = newIndex;
            RaiseCurrentTrackChanged();
        }
        return CurrentTrack;
    }

    private void RegenerateShuffleOrder()
    {
        var indices = Enumerable.Range(0, _queue.Count).ToList();
        for (var i = indices.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        _shuffleOrder = indices;
        _shufflePosition = _currentIndex >= 0 ? _shuffleOrder.IndexOf(_currentIndex) : -1;
    }

    private void RaiseCurrentTrackChanged() => CurrentTrackChanged?.Invoke(this, CurrentTrack);
}