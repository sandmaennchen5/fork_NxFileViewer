using System;
using System.Collections.Generic;
using Emignatik.NxFileViewer.Settings;
namespace Emignatik.NxFileViewer.Services.PackageOperations;
public enum PackageOperation { Merge, Convert, Split }
public static class PackageCommandArguments
{
    public static IReadOnlyList<string> BuildArguments(PackageEngine engine, PackageOperation operation,
        IReadOnlyList<string> sources, string output, string type, string keys, string? inputList = null)
    {
        if (type is not ("nsp" or "xci")) throw new ArgumentException("Unsupported target type.");
        var args = new List<string>();
        args.Add((engine, operation) switch
        {
            (PackageEngine.Rust, PackageOperation.Merge) => "--direct_multi",
            (PackageEngine.Rust, PackageOperation.Convert) => "--direct_creation",
            (PackageEngine.Rust, PackageOperation.Split) => "--dspl",
            (PackageEngine.Kronos, PackageOperation.Merge) => "--direct_multi",
            (PackageEngine.Kronos, PackageOperation.Convert) => "--direct_creation",
            (PackageEngine.Kronos, PackageOperation.Split) => "--direct_splitter",
            _ => throw new ArgumentOutOfRangeException(nameof(engine))
        });
        if (engine == PackageEngine.Kronos && operation == PackageOperation.Merge)
        {
            if (inputList == null) throw new ArgumentException("Kronos merge requires an input list.");
            args.AddRange(new[] { "calculate", "-tfile", inputList });
        }
        else args.AddRange(sources);
        args.AddRange(new[] { "-o", output, "-t", type });
        if (engine == PackageEngine.Kronos) args.AddRange(new[] { "-pv", "false", "-kp", "false" });
        if (engine == PackageEngine.Rust) args.AddRange(new[] { "--keys", keys });
        return args;
    }
}
