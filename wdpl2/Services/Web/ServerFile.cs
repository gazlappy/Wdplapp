namespace Wdpl2.Services.Web;

/// <summary>
/// One backend file, and where it is served from.
/// </summary>
/// <remarks>
/// Nearly always the two are the same, and a module lists a plain path. The
/// exception is a page that belongs at two addresses: the captains' scorecard
/// is also the cup tie card, and a cup tie has to be reached from the
/// competition side of the site rather than the league's, or the captains end
/// up hunting for it among their league fixtures. Writing it as
/// <c>source&gt;destination</c> keeps one file under source control and serves
/// it twice, rather than a second copy that would quietly drift.
/// </remarks>
public readonly record struct ServerFile(string Source, string Destination)
{
    private const char Separator = '>';

    public static ServerFile Parse(string entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry);

        var at = entry.IndexOf(Separator);
        if (at < 0) return new ServerFile(entry, entry);

        var source = entry[..at].Trim();
        var destination = entry[(at + 1)..].Trim();

        if (source.Length == 0 || destination.Length == 0)
            throw new ArgumentException($"'{entry}' is not a source>destination pair.", nameof(entry));

        return new ServerFile(source, destination);
    }

    /// <summary>True when this entry serves a file at a second address.</summary>
    public bool IsAlias => !Source.Equals(Destination, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => IsAlias ? $"{Source} > {Destination}" : Source;
}
