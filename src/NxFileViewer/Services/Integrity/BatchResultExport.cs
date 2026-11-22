using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace Emignatik.NxFileViewer.Services.Integrity;

public static class BatchResultExport
{
    // Stable identifiers match the table's SortMemberPath; values may use display properties.
    public static IReadOnlyList<BatchExportColumn> AllColumns { get; } = new BatchExportColumn[]
    {
        new("FileName", "File", "FileName"),
        new("FileType", "FileType", "FileType"),
        new("PackageType", "PackageType", "TypeDisplay"),
        new("Structure", "Structure", "StructureDisplay"),
        new("NcaSignature", "NcaSignature", "NcaSignature"),
        new("Integrity", "Integrity", "Integrity"),
        new("FileSize", "FileBytes", "FileSize"),
        new("Compression", "Compression", "Compression"),
        new("CompressionRatio", "CompressionRatio", "CompressionRatio"),
        new("SystemUpdateVersion", "SystemUpdateVersion", "SystemUpdateVersion"),
        new("ContentType", "ContentType", "ContentType"),
        new("TitleId", "TitleId", "TitleId"),
        new("VersionNumber", "Version", "Version"),
        new("SystemVersion", "Firmware", "SystemVersion"),
        new("BaseTitleId", "BaseTitleId", "BaseTitleId"),
        new("MasterKey", "MasterKey", "MasterKey"),
        new("MinimumApplicationVersion", "MinimumApplicationVersion", "MinimumApplicationVersion"),
        new("Distribution", "Distribution", "Distribution"),
        new("BuildId", "BuildId", "BuildId"),
        new("IsDemo", "Demo", "IsDemo"),
        new("PermissionDisplay", "Permission", "PermissionDisplay"),
        new("FileSystemPermissions", "FileSystemPermissions", "FileSystemPermissions"),
        new("AcidSignatureValidity", "AcidSignature", "AcidSignatureValidity"),
        new("Title", "Title", "Title"),
        new("Publisher", "Publisher", "Publisher"),
        new("DisplayVersion", "DisplayVersion", "DisplayVersion"),
        new("Languages", "Languages", "Languages"),
        new("Error", "Error", "Error"),
        new("MissingKeys", "MissingKeys", "MissingKeys"),
        new("FilePath", "Path", "FilePath"),
        new("NamingStatus", "Naming", "NamingStatus"),
        new("ProposedPath", "ProposedPath", "ProposedPath"),
        new("NamingError", "NamingError", "NamingError"),
        new("ConversionStatus", "Conversion", "ConversionStatus"),
        new("SourceSize", "SourceBytes", "SourceSize"),
        new("OutputSize", "OutputBytes", "OutputSize"),
        new("ConvertedPath", "ConvertedPath", "ConvertedPath")
    };

    public static IReadOnlyList<BatchExportColumn> SelectColumns(bool visibleOnly, string? layout)
    {
        if (!visibleOnly || string.IsNullOrWhiteSpace(layout)) return AllColumns;
        BatchColumnState[]? states;
        try { states = JsonSerializer.Deserialize<BatchColumnState[]>(layout); }
        catch (JsonException) { return AllColumns; }
        if (states == null) return AllColumns;
        var byId = AllColumns.ToDictionary(column => column.Id, StringComparer.Ordinal);
        return states.Where(state => state != null && state.Visible && state.Id != null && byId.ContainsKey(state.Id))
            .OrderBy(state => state.Order).DistinctBy(state => state.Id).Select(state => byId[state.Id]).ToArray();
    }

    public static string Value(BatchIntegrityResult result, BatchExportColumn column) =>
        Convert.ToString(typeof(BatchIntegrityResult).GetProperty(column.Property)!.GetValue(result), CultureInfo.InvariantCulture) ?? "";

    public static void WriteCsv(Stream stream, IEnumerable<BatchIntegrityResult> results, IReadOnlyList<BatchExportColumn> columns)
    {
        BatchCsvWriter.Write(stream, columns.Select(column => column.Header),
            results.Select(result => columns.Select(column => Value(result, column))));
    }

    public static void WriteExcel(Stream stream, IEnumerable<BatchIntegrityResult> results, IReadOnlyList<BatchExportColumn> columns) =>
        BatchExcelWriter.Write(stream, columns, results.Select(result => columns.Select(column => Value(result, column))));
}
