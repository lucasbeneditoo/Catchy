using System;
using System.IO;
using LibVLCSharp.Shared;

namespace Catchy.Services;

public class AudioPlayer : IDisposable
{
    private readonly LibVLC _libVLC;
    private readonly MediaPlayer _mediaPlayer;

    private Media? _currentMedia;
    private bool _disposed;

    public AudioPlayer()
    {
        Core.Initialize();

        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);

        _mediaPlayer.Playing += OnPlaying;
        _mediaPlayer.Paused += OnPaused;
        _mediaPlayer.Stopped += OnStopped;
        _mediaPlayer.EndReached += OnEndReached;
    }

    // Eventos de reprodução
    public event EventHandler? Playing;
    public event EventHandler? Paused;
    public event EventHandler? Stopped;
    public event EventHandler? EndReached;

    // Estado atual
    public VLCState State => _mediaPlayer.State;

    public bool IsPlaying => _mediaPlayer.IsPlaying;

    // Duração e posição
    public long Length => _mediaPlayer.Length;

    public bool IsSeekable => _mediaPlayer.IsSeekable;

    public float Position
    {
        get => _mediaPlayer.Position;
        set => _mediaPlayer.Position = Math.Clamp(value, 0f, 1f);
    }

    // Volume
    public int Volume
    {
        get => _mediaPlayer.Volume;
        set => _mediaPlayer.Volume = Math.Clamp(value, 0, 100);
    }

    // Reprodução
    public void Play(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                "Arquivo de áudio não encontrado.",
                filePath
            );

        _mediaPlayer.Stop();

        _currentMedia?.Dispose();

        _currentMedia = new Media(
            _libVLC,
            filePath,
            FromType.FromPath
        );

        if (!_mediaPlayer.Play(_currentMedia))
        {
            _currentMedia.Dispose();
            _currentMedia = null;

            throw new InvalidOperationException(
                "Não foi possível reproduzir o arquivo."
            );
        }
    }

    public void Resume()
    {
        if (!_disposed && _mediaPlayer.State == VLCState.Paused)
            _mediaPlayer.Play();
    }

    public void Pause()
    {
        if (!_disposed && _mediaPlayer.IsPlaying)
            _mediaPlayer.Pause();
    }

    public void Stop()
    {
        if (!_disposed)
            _mediaPlayer.Stop();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _mediaPlayer.Playing -= OnPlaying;
        _mediaPlayer.Paused -= OnPaused;
        _mediaPlayer.Stopped -= OnStopped;
        _mediaPlayer.EndReached -= OnEndReached;

        _mediaPlayer.Stop();

        _currentMedia?.Dispose();
        _mediaPlayer.Dispose();
        _libVLC.Dispose();
    }

    // Encaminhamento dos eventos
    private void OnPlaying(object? sender, EventArgs e)
        => Playing?.Invoke(this, e);

    private void OnPaused(object? sender, EventArgs e)
        => Paused?.Invoke(this, e);

    private void OnStopped(object? sender, EventArgs e)
        => Stopped?.Invoke(this, e);

    private void OnEndReached(object? sender, EventArgs e)
        => EndReached?.Invoke(this, e);
}
