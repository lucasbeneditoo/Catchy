using System.IO;

namespace Catchy.Models;

public class Song
{
    public string FilePath { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string Genre { get; set; } = "";
    public uint Year { get; set; }
    public uint Track { get; set; }

    public byte[]? Cover { get; set; }

    public static Song ReadSong(string caminho)
    {
        using var file = TagLib.File.Create(caminho);

        return new Song
        {
            FilePath = caminho,

            Title = file.Tag.Title
                    ?? Path.GetFileName(caminho),

            Artist = file.Tag.Performers.Length > 0
                ? file.Tag.Performers[0]
                : "Artista desconhecido",

            Album = file.Tag.Album
                    ?? "Álbum desconhecido",

            Genre = file.Tag.Genres.Length > 0
                ? file.Tag.Genres[0]
                : "Gênero desconhecido",

            Year = file.Tag.Year,
            Track = file.Tag.Track,

            Cover = file.Tag.Pictures.Length > 0
                ? file.Tag.Pictures[0].Data.Data
                : null
        };
    }
}