using System.Collections.Generic;

namespace Emignatik.NxFileViewer.Services.KeysManagement;

public sealed record KeyFileValidationResult(
    bool FileExists,
    int ValidEntryCount,
    IReadOnlyList<string> MissingKeys,
    IReadOnlyList<string> InvalidKeys,
    IReadOnlyList<int> InvalidLineNumbers,
    int? HighestValidMasterKeyRevision = null,
    IReadOnlyList<string>? UnsupportedMasterKeys = null)
{
    public IReadOnlyDictionary<string, ConsoleKeyStatus>? ConsoleKeys { get; init; }

    public bool IsValid => FileExists && ValidEntryCount > 0 && MissingKeys.Count == 0 && InvalidKeys.Count == 0 && InvalidLineNumbers.Count == 0;

    public bool HasWarnings => UnsupportedMasterKeys is { Count: > 0 };
}

public enum ConsoleKeyStatus { Missing, InvalidFormat, Present }
