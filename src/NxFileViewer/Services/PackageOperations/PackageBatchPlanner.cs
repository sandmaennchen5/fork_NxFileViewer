using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Emignatik.NxFileViewer.Services.Integrity;

namespace Emignatik.NxFileViewer.Services.PackageOperations;

public enum PackageBatchFormat { KeepSource, Nsp, Xci }
public enum PackageMergeGrouping { ByGame, AcrossGames }
public enum PackageBatchSkipReason { UnknownGame, MultipleGames, SingleFile }
public sealed record PackageJob(PackageOperation Operation, string Type, string[] Sources, string? ExpectedGameId = null);
public sealed record PackageBatchSkipped(string Source, PackageBatchSkipReason Reason);
public sealed record PackageBatchPlan(PackageJob[] Jobs, PackageBatchSkipped[] Skipped);

public static class PackageBatchPlanner
{
    public static string TargetFormat(string source, PackageBatchFormat format) => format switch
    {
        PackageBatchFormat.Nsp => "nsp",
        PackageBatchFormat.Xci => "xci",
        PackageBatchFormat.KeepSource => Path.GetExtension(source).ToLowerInvariant() switch
        {
            ".nsp" or ".nsz" => "nsp",
            ".xci" or ".xcz" => "xci",
            _ => throw new ArgumentException("Unsupported source format.")
        },
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public static PackageBatchPlan Split(IEnumerable<BatchIntegrityResult> inputs, PackageBatchFormat format) =>
        new(inputs.GroupBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.FirstOrDefault(r => r.HasMultiplePackagesInSource))
            .Where(r => r != null)
            .Select(r => new PackageJob(PackageOperation.Split, TargetFormat(r!.FilePath, format), new[] { r.FilePath })).ToArray(),
            Array.Empty<PackageBatchSkipped>());

    private static IEnumerable<BatchPackageSummary> Contents(BatchIntegrityResult source) => source.Packages.Length > 0
        ? source.Packages : new[] { new BatchPackageSummary("", source.TitleId, source.ContentType, source.Version) { BaseTitleId = source.BaseTitleId } };
    private static BatchIntegrityResult[] SourceFiles(IEnumerable<BatchIntegrityResult> inputs) =>
        inputs.GroupBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase).Select(g =>
        {
            var first = g.OrderByDescending(r => r.Packages.Length).First();
            return first with { Packages = g.SelectMany(r => r.Packages).DistinctBy(p => (p.TitleId.ToUpperInvariant(), p.Version)).ToArray() };
        }).ToArray();
    public static BatchIntegrityResult[] FindMultiGameMergeCandidates(IEnumerable<BatchIntegrityResult> inputs)
    {
        var sources = SourceFiles(inputs);
        static string[] Ids(BatchIntegrityResult source) => source.Packages.Select(p =>
            SourceGameId(p.BaseTitleId, p.TitleId, p.ContentType)).Where(id => id != null).Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return sources.Where(source =>
        {
            var ids = Ids(source);
            if (ids.Length < 2 || source.SourcePackageCount > source.Packages.Length) return false;
            var contained = source.Packages.Select(p => (p.TitleId.ToUpperInvariant(), p.Version)).ToHashSet();
            return sources.Where(other => !other.FilePath.Equals(source.FilePath, StringComparison.OrdinalIgnoreCase))
                .Any(other => Contents(other).Any(p => ids.Contains(SourceGameId(p.BaseTitleId, p.TitleId, p.ContentType), StringComparer.OrdinalIgnoreCase) &&
                    !contained.Contains((p.TitleId.ToUpperInvariant(), p.Version))));
        }).ToArray();
    }
    public static PackageBatchPlan Merge(IEnumerable<BatchIntegrityResult> inputs, PackageMergeGrouping grouping, PackageBatchFormat format)
    {
        if (format == PackageBatchFormat.KeepSource) throw new ArgumentException("Merge needs an explicit target format.");
        var sources = inputs.GroupBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase).Select(g =>
        {
            var first = g.OrderByDescending(r => r.Packages.Length).First();
            var packages = g.SelectMany(r => r.Packages).DistinctBy(p => (p.TitleId, p.Version)).ToArray();
            return first with { Packages = packages };
        }).ToArray();
        var type = TargetFormat("", format);
        if (grouping == PackageMergeGrouping.AcrossGames)
            return new(sources.Length > 1 ? new[] { new PackageJob(PackageOperation.Merge, type, sources.Select(r => r.FilePath).ToArray()) }
                : sources.Length == 1 && sources[0].HasMultiplePackagesInSource
                    ? new[] { new PackageJob(PackageOperation.Convert, type, new[] { sources[0].FilePath }) } : Array.Empty<PackageJob>(),
                sources.Length == 1 && !sources[0].HasMultiplePackagesInSource
                    ? new[] { new PackageBatchSkipped(sources[0].FilePath, PackageBatchSkipReason.SingleFile) } : Array.Empty<PackageBatchSkipped>());
        var skipped = new List<PackageBatchSkipped>();
        var grouped = new Dictionary<string, List<BatchIntegrityResult>>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            var ids = source.Packages.Length > 0
                ? source.Packages.Select(p => SourceGameId(p.BaseTitleId, p.TitleId, p.ContentType)).ToArray()
                : new[] { SourceGameId(source.BaseTitleId, source.TitleId, source.ContentType) };
            if (ids.Any(id => id == null) || source.SourcePackageCount > source.Packages.Length)
            {
                skipped.Add(new(source.FilePath, PackageBatchSkipReason.UnknownGame));
                continue;
            }
            var distinct = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (distinct.Length != 1)
            {
                skipped.Add(new(source.FilePath, PackageBatchSkipReason.MultipleGames));
                continue;
            }
            if (!grouped.TryGetValue(distinct[0]!, out var group)) grouped[distinct[0]!] = group = new();
            group.Add(source);
        }
        var jobs = new List<PackageJob>();
        foreach (var (gameId, group) in grouped)
        {
            if (group.Count > 1) jobs.Add(new(PackageOperation.Merge, type, group.Select(r => r.FilePath).ToArray(), gameId));
            else if (group[0].HasMultiplePackagesInSource) jobs.Add(new(PackageOperation.Convert, type, new[] { group[0].FilePath }, gameId));
            else skipped.Add(new(group[0].FilePath, PackageBatchSkipReason.SingleFile));
        }
        return new(jobs.ToArray(), skipped.ToArray());
    }

    internal static string? SourceGameId(string baseId, string titleId, string contentType)
    {
        static bool Valid(string value) => value.Length == 16 && value.All(Uri.IsHexDigit) && value != "0000000000000000";
        if (Valid(baseId)) return baseId.ToUpperInvariant();
        return contentType is "Base" or "Application" && Valid(titleId) ? titleId.ToUpperInvariant() : null;
    }
}