using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Catchy.Models;

namespace Catchy.Services;

public class AudioLibrary
{
    private static readonly HashSet<string> SupportedExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".flac",
        ".wav",
        ".ogg",
        ".oga",
        ".opus",
        ".m4a",
        ".m4b",
        ".aac",
        ".wma",
        ".aiff",
        ".aif",
        ".ape",
        ".wv",
        ".tta"
    };

    public static List<Song> LoadSongs(string folderPath)
    {
        var songs = new List<Song>();

        if (!Directory.Exists(folderPath))
            return songs;

        var files = Directory
            .EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(
                Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            try
            {
                songs.Add(Song.ReadSong(file));
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Não foi possível carregar {file}: {ex.Message}");
            }
        }

        return songs;
    }
}