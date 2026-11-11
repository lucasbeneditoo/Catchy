using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentIcons.Common;
using LibVLCSharp.Shared;
using Catchy.Models;
using Catchy.Services;

namespace Catchy.Views;

public partial class MainWindow : Window
{
    private readonly AudioPlayer _audioPlayer;
    private readonly PlaybackQueue _queue;
    private readonly DispatcherTimer _progressTimer;

    private static readonly HashSet<string> SupportedFormats = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".flac",
        ".wav",
        ".ogg",
        ".m4a",
        ".aac",
        ".wma",
        ".opus"
    };

    private bool _isSeeking;
    private bool _wasPlayingBeforeSeek;

    private bool _updatingProgress;

    private float _pendingPosition;
    private bool _hasPendingSeek;

    // Controle de volume
    private bool _volumeSliderRevealed;
    private bool _volumeInteracted;
    private bool _isMuted;
    private int _previousVolume = 50;

    public MainWindow()
    {
        InitializeComponent();

        _audioPlayer = new AudioPlayer();
        _audioPlayer.Volume = (int)VolumeSlider.Value;

        _previousVolume = _audioPlayer.Volume;
        UpdateVolumeIcon(_audioPlayer.Volume);

        AddHandler(
            InputElement.PointerPressedEvent,
            Window_PointerPressed,
            RoutingStrategies.Tunnel
        );

        AddHandler(
            InputElement.PointerReleasedEvent,
            Window_PointerReleased,
            RoutingStrategies.Tunnel
        );

        _queue = new PlaybackQueue();

        _audioPlayer.Playing += AudioPlayer_Playing;
        _audioPlayer.Paused += AudioPlayer_Paused;
        _audioPlayer.Stopped += AudioPlayer_Stopped;
        _audioPlayer.EndReached += AudioPlayer_EndReached;

        LoadLibrary();

        UpdatePlaybackModeIcons();

        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };

        _progressTimer.Tick += UpdateProgress;
        _progressTimer.Start();
    }

    // Carrega a biblioteca musical
    private void LoadLibrary()
    {
        var audioFolder = Path.Combine(
            AppContext.BaseDirectory,
            "Audio"
        );

        if (!Directory.Exists(audioFolder))
        {
            Console.WriteLine($"Pasta não encontrada: {audioFolder}");
            return;
        }

        var songs = new List<Song>();

        foreach (var caminho in Directory.EnumerateFiles(
            audioFolder,
            "*",
            SearchOption.AllDirectories))
        {
            if (!SupportedFormats.Contains(Path.GetExtension(caminho)))
                continue;

            try
            {
                var song = Song.ReadSong(caminho);
                songs.Add(song);

                Console.WriteLine(
                    $"Música encontrada: {song.Artist} - {song.Title}"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Erro ao ler música {caminho}: {ex.Message}"
                );
            }
        }

        _queue.SetQueue(songs);

        Console.WriteLine($"Total de músicas: {songs.Count}");

        UpdateSongInfo(_queue.Current);
    }

    // Atualiza as informações da música
    private void UpdateSongInfo(Song? song)
    {
        if (song == null)
        {
            SongNameText.Text = "Nenhuma música";
            ArtistNameText.Text = "";
            CoverImage.Source = null;
            return;
        }

        SongNameText.Text = song.Title;
        ArtistNameText.Text = song.Artist;

        CoverImage.Source = null;

        if (song.Cover != null)
        {
            using var stream = new MemoryStream(song.Cover);
            CoverImage.Source = new Bitmap(stream);
        }
    }

    // Reproduz uma música
    private void PlaySong(Song song)
    {
        UpdateSongInfo(song);

        _updatingProgress = true;
        ProgressSlider.Value = 0;
        _updatingProgress = false;

        _audioPlayer.Play(song.FilePath);

        if (_hasPendingSeek)
        {
            _pendingPosition = Math.Clamp(_pendingPosition, 0f, 1f);
        }
    }

    // Botão play/pause
    private void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        switch (_audioPlayer.State)
        {
            case VLCState.Playing:
                _audioPlayer.Pause();
                break;

            case VLCState.Paused:
                _audioPlayer.Resume();
                break;

            default:
                if (_queue.Current != null)
                    PlaySong(_queue.Current);
                break;
        }
    }

    // Próxima música
    private void NextButton_Click(object? sender, RoutedEventArgs e)
    {
        var nextSong = _queue.Next();

        if (nextSong != null)
            PlaySong(nextSong);
    }

    // Música anterior
    private void PreviousButton_Click(object? sender, RoutedEventArgs e)
    {
        var previousSong = _queue.Previous();

        if (previousSong != null)
            PlaySong(previousSong);
    }

    // Atualiza a seekbar
    private void UpdateProgress(object? sender, EventArgs e)
    {
        if (_isSeeking)
            return;

        if (!_audioPlayer.IsSeekable)
            return;

        if (_audioPlayer.State != VLCState.Playing &&
            _audioPlayer.State != VLCState.Paused)
            return;

        _updatingProgress = true;
        ProgressSlider.Value = _audioPlayer.Position;
        _updatingProgress = false;
    }

    // Alteração visual da seekbar
    private void ProgressSlider_ValueChanged(
        object? sender,
        Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_updatingProgress || _isSeeking)
            return;

        float position = (float)e.NewValue;

        if (_audioPlayer.State == VLCState.Playing ||
            _audioPlayer.State == VLCState.Paused)
        {
            if (!_audioPlayer.IsSeekable)
                return;

            _audioPlayer.Position = position;
        }
        else
        {
            _pendingPosition = position;
            _hasPendingSeek = true;
        }
    }

    // Atualiza o ícone de reprodução
    private void SetPlayIcon(Symbol symbol)
    {
        Dispatcher.UIThread.Post(() =>
        {
            PlayIcon.Symbol = symbol;
        });
    }

    // Atualiza o volume pelo slider
    private void VolumeSlider_ValueChanged(
        object? sender,
        Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        int volume = (int)Math.Round(e.NewValue);

        if (_audioPlayer is not null)
            _audioPlayer.Volume = volume;

        if (volume > 0)
        {
            _previousVolume = volume;
            _isMuted = false;
        }
        else
        {
            _isMuted = true;
        }

        if (VolumeIcon is not null)
            UpdateVolumeIcon(volume);
    }

    // Mostra o slider ao passar pelo botão de volume
    private void VolumeButton_PointerEntered(
        object? sender,
        PointerEventArgs e)
    {
        _volumeSliderRevealed = true;
        SetVolumeSliderVisibility(true);
    }

    private void Window_PointerReleased(
        object? sender,
        PointerReleasedEventArgs e)
    {
        if (!_isSeeking)
            return;

        float position = (float)ProgressSlider.Value;

        long duration = _audioPlayer.Length;

        if (duration > 1000)
        {
            float maxPosition = (duration - 1000f) / duration;
            position = Math.Clamp(position, 0f, maxPosition);
        }

        _audioPlayer.Position = position;

        bool resume = _wasPlayingBeforeSeek;

        _isSeeking = false;
        _wasPlayingBeforeSeek = false;

        if (resume)
            _audioPlayer.Resume();

        // Atualiza a posição visual
        _updatingProgress = true;
        ProgressSlider.Value = position;
        _updatingProgress = false;
    }

    // Detecta quando o mouse sai do miniplayer
    private void MiniPlayer_PointerExited(
        object? sender,
        PointerEventArgs e)
    {
        // Se já houve interação, mantém o comportamento de clique fora
        if (_volumeInteracted)
            return;

        _volumeSliderRevealed = false;
        SetVolumeSliderVisibility(false);
    }

    // Marca interação ao pressionar o slider
    private void VolumeSlider_PointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        _volumeInteracted = true;
    }

    // Esconde somente ao clicar fora do conjunto de volume
    private void Window_PointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        // Detecta o início da interação com a seekbar
        var seekPosition = e.GetPosition(ProgressSlider);

        bool insideSeekbar =
            seekPosition.X >= 0 &&
            seekPosition.Y >= 0 &&
            seekPosition.X <= ProgressSlider.Bounds.Width &&
            seekPosition.Y <= ProgressSlider.Bounds.Height;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            insideSeekbar &&
            _audioPlayer.IsSeekable &&
            (_audioPlayer.State == VLCState.Playing ||
            _audioPlayer.State == VLCState.Paused))
        {
            if (!_isSeeking)
            {
                _isSeeking = true;
                _wasPlayingBeforeSeek = _audioPlayer.IsPlaying;

                if (_wasPlayingBeforeSeek)
                    _audioPlayer.Pause();
            }
        }


        // Mantém o funcionamento anterior do volume
        if (!_volumeSliderRevealed)
            return;

        var position = e.GetPosition(VolumeControl);

        bool insideVolumeControl =
            position.X >= 0 &&
            position.Y >= 0 &&
            position.X <= VolumeControl.Bounds.Width &&
            position.Y <= VolumeControl.Bounds.Height;

        if (insideVolumeControl)
        {
            _volumeInteracted = true;
            return;
        }

        _volumeSliderRevealed = false;
        _volumeInteracted = false;
        SetVolumeSliderVisibility(false);
    }


    // Controla a visibilidade do slider
    private void SetVolumeSliderVisibility(bool visible)
    {
        VolumeSlider.Opacity = visible ? 1 : 0;
        VolumeSlider.IsHitTestVisible = visible;
    }

    // Botão de volume: mute e restauração
    private void VolumeButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        _volumeInteracted = true;

        if (!_isMuted)
        {
            if (_audioPlayer.Volume > 0)
                _previousVolume = _audioPlayer.Volume;

            _isMuted = true;

            VolumeSlider.Value = 0;
            _audioPlayer.Volume = 0;

            UpdateVolumeIcon(0);
        }
        else
        {
            int volumeToRestore = _previousVolume > 0
                ? _previousVolume
                : 50;

            _isMuted = false;

            VolumeSlider.Value = volumeToRestore;
            _audioPlayer.Volume = volumeToRestore;

            UpdateVolumeIcon(volumeToRestore);
        }
    }

    // Atualiza o ícone de volume
    private void UpdateVolumeIcon(int volume)
    {
        VolumeIcon.Symbol = volume switch
        {
            0 => Symbol.SpeakerOff,
            <= 50 => Symbol.Speaker1,
            _ => Symbol.Speaker2
        };
    }

    private void UpdatePlaybackModeIcons()
    {
        RepeatIcon.Symbol = _queue.Repeat switch
        {
            RepeatMode.One => Symbol.ArrowRepeat1,
            _ => Symbol.ArrowRepeatAll
        };

        RepeatIcon.Opacity = _queue.Repeat == RepeatMode.Off ? 0.4 : 1.0;

        ShuffleIcon.Opacity = _queue.Shuffle ? 1.0 : 0.4;
    }

    // Eventos do AudioPlayer
    private void AudioPlayer_Playing(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_hasPendingSeek && _audioPlayer.IsSeekable)
            {
                _audioPlayer.Position = _pendingPosition;
                _hasPendingSeek = false;
            }

            PlayIcon.Symbol = Symbol.Pause;
        });
    }

    private void AudioPlayer_Paused(object? sender, EventArgs e)
    {
        SetPlayIcon(Symbol.Play);
    }

    private void AudioPlayer_Stopped(object? sender, EventArgs e)
    {
        SetPlayIcon(Symbol.Play);
    }

    private void RepeatButton_Click(object? sender, RoutedEventArgs e)
    {
        _queue.Repeat = _queue.Repeat switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            _ => RepeatMode.Off
        };

        UpdatePlaybackModeIcons();
    }

    private void ShuffleButton_Click(object? sender, RoutedEventArgs e)
    {
        _queue.Shuffle = !_queue.Shuffle;

        UpdatePlaybackModeIcons();
    }

    // Ao terminar uma música, avança automaticamente
    private void AudioPlayer_EndReached(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var nextSong = _queue.Next(automatic: true);

            if (nextSong != null)
            {
                PlaySong(nextSong);
                return;
            }

            PlayIcon.Symbol = Symbol.Play;

            _updatingProgress = true;
            ProgressSlider.Value = 0;
            _updatingProgress = false;
        });
    }

    // Libera os recursos
    protected override void OnClosed(EventArgs e)
    {
        _progressTimer.Stop();

        _audioPlayer.Playing -= AudioPlayer_Playing;
        _audioPlayer.Paused -= AudioPlayer_Paused;
        _audioPlayer.Stopped -= AudioPlayer_Stopped;
        _audioPlayer.EndReached -= AudioPlayer_EndReached;

        _audioPlayer.Dispose();

        base.OnClosed(e);
    }
}