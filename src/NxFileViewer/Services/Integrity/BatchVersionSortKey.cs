using System;
using System.Linq;
using Emignatik.NxFileViewer.Services.Updates;

namespace Emignatik.NxFileViewer.Services.Integrity;

// WPF's property comparer uses the non-generic IComparable interface.
public sealed class BatchVersionSortKey : IComparable, IComparable<BatchVersionSortKey>
{
    private readonly string _text;
    private readonly BatchVersionSortKey[]? _parts;
    private readonly Version? _numeric;
    private readonly ViewerVersion? _semantic;

    public BatchVersionSortKey(string? text)
    {
        _text = text?.Trim() ?? "";
        if (_text.Contains(" / ", StringComparison.Ordinal))
        {
            _parts = _text.Split(" / ").Select(value => new BatchVersionSortKey(value)).ToArray();
            return;
        }
        if (ViewerVersion.TryParse(_text, out var semantic))
        {
            _semantic = semantic;
            _numeric = Normalize(semantic.Numeric);
        }
        else if (Version.TryParse(_text.TrimStart('v', 'V'), out var numeric))
        {
            _numeric = Normalize(numeric);
            _semantic = ViewerVersion.FromNumeric(numeric);
        }
    }

    private static Version Normalize(Version value) =>
        new(value.Major, value.Minor, Math.Max(0, value.Build), Math.Max(0, value.Revision));

    public int CompareTo(BatchVersionSortKey? other)
    {
        if (other == null) return 1;
        if (_parts != null || other._parts != null)
        {
            var left = _parts ?? new[] { this };
            var right = other._parts ?? new[] { other };
            for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
            {
                var part = left[i].CompareTo(right[i]);
                if (part != 0) return part;
            }
            return left.Length.CompareTo(right.Length);
        }
        if (_numeric != null && other._numeric != null)
        {
            var core = _numeric.CompareTo(other._numeric);
            return core != 0 ? core : _semantic!.CompareTo(other._semantic);
        }
        // Missing and non-version labels remain deterministic and do not interrupt sorting.
        if ((_numeric != null) != (other._numeric != null)) return _numeric != null ? 1 : -1;
        return StringComparer.OrdinalIgnoreCase.Compare(_text, other._text);
    }

    int IComparable.CompareTo(object? obj) => obj == null ? 1 : obj is BatchVersionSortKey other
        ? CompareTo(other) : throw new ArgumentException("Expected a version sort key.", nameof(obj));
}