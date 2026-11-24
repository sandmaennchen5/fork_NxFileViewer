using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
namespace NxFileViewer.Avalonia;
internal static class IconCache
{
    private static readonly Dictionary<string, IImage> Images = new();
    internal static IImage Get(string path)
    {
        if (Images.TryGetValue(path, out var image)) return image;
        using var stream = AssetLoader.Open(new Uri("avares://NxFileViewerFork" + path));
        return Images[path] = new Bitmap(stream);
    }
}
