using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Emignatik.NxFileViewer.Services.Integrity;

public sealed record BatchColumnState(string Id, int Order, bool Visible, double Width, DataGridLengthUnitType WidthUnit);

public static class BatchColumnLayout
{
    public static string Capture(IEnumerable<DataGridColumn> columns) => JsonSerializer.Serialize(columns.Select(column =>
        new BatchColumnState(column.SortMemberPath, column.DisplayIndex, column.Visibility == Visibility.Visible,
            column.Width.Value, column.Width.UnitType)));

    public static void Apply(IEnumerable<DataGridColumn> columns, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        BatchColumnState[]? saved;
        try { saved = JsonSerializer.Deserialize<BatchColumnState[]>(json); }
        catch (JsonException) { return; }
        if (saved == null) return;
        var all = columns.ToArray();
        var byId = all.ToDictionary(column => column.SortMemberPath, StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<DataGridColumn>();
        foreach (var state in saved.Where(state => state != null).OrderBy(state => state.Order))
        {
            if (state.Id == null || !byId.TryGetValue(state.Id, out var column) || !used.Add(state.Id)) continue;
            column.Visibility = state.Visible ? Visibility.Visible : Visibility.Collapsed;
            if (double.IsFinite(state.Width) && state.Width >= 0 && state.Width <= 10000 &&
                Enum.IsDefined(state.WidthUnit) && (state.WidthUnit != DataGridLengthUnitType.Star || state.Width > 0))
                column.Width = new DataGridLength(state.Width, state.WidthUnit);
            ordered.Add(column);
        }
        // Neue Spalten behalten ihre Vorgaben, auch bei Layouts einer älteren Version.
        ordered.AddRange(all.Where(column => !used.Contains(column.SortMemberPath)));
        for (var i = 0; i < ordered.Count; i++) ordered[i].DisplayIndex = i;
    }
}
