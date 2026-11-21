using System;
using System.Collections.Generic;
using System.IO;
using Emignatik.NxFileViewer.Settings;
namespace Emignatik.NxFileViewer.Services.Nsz;
public enum NszOperation { Compress, Decompress }
public static class NszCommandArguments
{
    public static IReadOnlyList<string> BuildRustArguments(string source, string output, string keys, int level,
        NszOperation operation = NszOperation.Compress)
    {
        var args = new List<string> { operation == NszOperation.Compress ? "--compress" : "--decompress",
            source, "--keys", keys, "-o", output };
        if (operation == NszOperation.Compress)
            args.AddRange(new[] { "--level", Math.Clamp(level, 1, 22).ToString(System.Globalization.CultureInfo.InvariantCulture) });
        return args;
    }

    public static IReadOnlyList<string> BuildArguments(string source, string output, string keys,
        NszOperation operation, int level, NszCompressionMode mode = NszCompressionMode.Auto, int blockSizeExponent = 20, bool useRelativePaths = false)
    {
        var args = new List<string> { operation == NszOperation.Compress ? "-C" : "-D", "--keys", keys, "-o", output };
        if (operation == NszOperation.Compress)
        {
            if (mode == NszCompressionMode.Solid) args.Add("--solid");
            else if (mode == NszCompressionMode.Block)
                args.AddRange(new[] { "--block", "--bs", Math.Clamp(blockSizeExponent, 14, 32).ToString(System.Globalization.CultureInfo.InvariantCulture) });
            // Preserve extra partitions/files; NSZ additionally verifies its own round trip.
            args.AddRange(new[] { "-K", "-V", "-l", Math.Clamp(level, 1, 22).ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }
        args.Add(useRelativePaths ? source : Path.GetFullPath(source));
        return args;
    }
}
