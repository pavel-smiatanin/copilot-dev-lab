namespace UrlShortener.Application.Abstract.Primary.Exceptions;

public sealed class AliasConflictException : Exception
{
    public string ConflictingAlias { get; }

    public IReadOnlyList<string> Suggestions { get; }

    public AliasConflictException(string conflictingAlias, IReadOnlyList<string> suggestions)
        : base($"Alias '{conflictingAlias}' is already taken.")
    {
        ConflictingAlias = conflictingAlias;
        Suggestions = suggestions;
    }
}
