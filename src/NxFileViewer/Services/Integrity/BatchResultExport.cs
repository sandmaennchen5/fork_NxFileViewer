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

public sealed record BatchExportColumn(string Id, string Header, string Property);

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
        using var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true);
        void Row(IEnumerable<string> values) => writer.Write(string.Join(";", values.Select(value => "\"" + value.Replace("\"", "\"\"") + "\"")) + "\r\n");
        Row(columns.Select(column => column.Header));
        foreach (var result in results) Row(columns.Select(column => Value(result, column)));
    }

    // A small streaming Open XML workbook: text cells preserve title IDs and never become formulas.
    public static void WriteExcel(Stream stream, IEnumerable<BatchIntegrityResult> results, IReadOnlyList<BatchExportColumn> columns)
    {
        if (columns.Count == 0) throw new InvalidOperationException("No export columns selected.");
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        void Part(string name, string xml)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write(xml);
        }
        Part("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
        Part("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Part("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Results\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Part("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
        Part("xl/styles.xml", "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
        using var sheet = zip.CreateEntry("xl/worksheets/sheet1.xml").Open();
        using var xml = XmlWriter.Create(sheet, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        void Start(string name) => xml.WriteStartElement(name, ns);
        Start("worksheet");
        Start("sheetViews"); Start("sheetView"); xml.WriteAttributeString("workbookViewId", "0");
        Start("pane"); xml.WriteAttributeString("ySplit", "1"); xml.WriteAttributeString("topLeftCell", "A2"); xml.WriteAttributeString("activePane", "bottomLeft"); xml.WriteAttributeString("state", "frozen"); xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
        Start("cols");
        for (var i = 0; i < columns.Count; i++)
        {
            Start("col"); xml.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture)); xml.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
            xml.WriteAttributeString("width", columns[i].Id is "FileName" or "FilePath" or "Title" ? "45" : "22"); xml.WriteAttributeString("customWidth", "1"); xml.WriteEndElement();
        }
        xml.WriteEndElement(); Start("sheetData");
        var row = 0;
        void Row(IEnumerable<string> values, bool header = false)
        {
            if (++row > 1048576) throw new InvalidOperationException("Excel worksheet row limit exceeded. Use CSV for this export.");
            Start("row"); xml.WriteAttributeString("r", row.ToString(CultureInfo.InvariantCulture));
            foreach (var value in values)
            {
                if (value.Length > 32767) throw new InvalidOperationException("Excel cell text limit exceeded. Use CSV for this export.");
                Start("c"); xml.WriteAttributeString("t", "inlineStr"); if (header) xml.WriteAttributeString("s", "1");
                Start("is"); Start("t"); xml.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                // XML 1.0 forbids some control characters that may occur in diagnostics.
                xml.WriteString(new string(value.Where(ch => XmlConvert.IsXmlChar(ch) || char.IsSurrogate(ch)).ToArray()));
                xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        Row(columns.Select(column => column.Header), true);
        foreach (var result in results) Row(columns.Select(column => Value(result, column)));
        xml.WriteEndElement();
        Start("autoFilter"); xml.WriteAttributeString("ref", $"A1:{ColumnName(columns.Count)}{row}"); xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static string ColumnName(int number)
    {
        var name = "";
        while (number > 0) { number--; name = (char)('A' + number % 26) + name; number /= 26; }
        return name;
    }
}
