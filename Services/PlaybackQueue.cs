using System;
using System.Collections.Generic;
using Catchy.Models;

namespace Catchy.Services;

public enum RepeatMode
{
    Off,
    All,
    One
}

public class PlaybackQueue
{
    private readonly List<Song> _songs = new();
    private readonly Stack<int> _history = new();
    private readonly List<int> _shuffleBag = new();
    private readonly Random _random = new();

    private int _currentIndex = -1;
    private bool _shuffle;

    public IReadOnlyList<Song> Songs => _songs;

    public Song? Current =>
        _currentIndex >= 0 && _currentIndex < _songs.Count
            ? _songs[_currentIndex]
            : null;

    public bool Shuffle
    {
        get => _shuffle;
        set
        {
            if (_shuffle == value)
                return;

            _shuffle = value;
            _history.Clear();
            RebuildShuffleBag();
        }
    }

    public RepeatMode Repeat { get; set; } = RepeatMode.Off;

    public void SetQueue(IEnumerable<Song> songs, int startIndex = 0)
    {
        _songs.Clear();
        _songs.AddRange(songs);

        _history.Clear();
        _shuffleBag.Clear();

        if (_songs.Count == 0)
        {
            _currentIndex = -1;
            return;
        }

        _currentIndex = Math.Clamp(startIndex, 0, _songs.Count - 1);

        RebuildShuffleBag();
    }

    public Song? Next(bool automatic = false)
    {
        if (Current == null)
            return null;

        if (automatic && Repeat == RepeatMode.One)
            return Current;

        if (Shuffle)
        {
            if (_shuffleBag.Count == 0)
            {
                if (Repeat != RepeatMode.All)
                    return null;

                RebuildShuffleBag();
            }

            if (_shuffleBag.Count == 0)
                return null;

            _history.Push(_currentIndex);

            _currentIndex = _shuffleBag[0];
            _shuffleBag.RemoveAt(0);

            return Current;
        }

        if (_currentIndex < _songs.Count - 1)
        {
            _history.Push(_currentIndex);
            _currentIndex++;

            return Current;
        }

        if (Repeat == RepeatMode.All)
        {
            _history.Push(_currentIndex);
            _currentIndex = 0;

            return Current;
        }

        return null;
    }

    public Song? Previous()
    {
        if (Current == null)
            return null;

        if (_history.Count > 0)
        {
            if (Shuffle)
                _shuffleBag.Insert(0, _currentIndex);

            _currentIndex = _history.Pop();

            return Current;
        }

        if (Repeat == RepeatMode.All)
        {
            if (Shuffle && _shuffleBag.Count > 0)
            {
                int last = _shuffleBag.Count - 1;
                _currentIndex = _shuffleBag[last];
                _shuffleBag.RemoveAt(last);

                return Current;
            }

            if (!Shuffle)
            {
                _currentIndex = _songs.Count - 1;
                return Current;
            }
        }

        return null;
    }

    private void RebuildShuffleBag()
    {
        _shuffleBag.Clear();

        if (!Shuffle || Current == null)
            return;

        for (int i = 0; i < _songs.Count; i++)
        {
            if (i != _currentIndex)
                _shuffleBag.Add(i);
        }

        for (int i = _shuffleBag.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);

            (_shuffleBag[i], _shuffleBag[j]) =
                (_shuffleBag[j], _shuffleBag[i]);
        }
    }
}