using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Emignatik.NxFileViewer.Services.Integrity;

internal static class BatchCsvWriter
{
    internal static void Write(Stream stream, IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
    {
        using var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true);
        void Row(IEnumerable<string> values) =>
            writer.Write(string.Join(";", values.Select(value => "\"" + value.Replace("\"", "\"\"") + "\"")) + "\r\n");
        Row(headers);
        foreach (var row in rows) Row(row);
    }
}
