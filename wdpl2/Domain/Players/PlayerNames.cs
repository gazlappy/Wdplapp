namespace Wdpl2.Domain.Players;

/// <summary>
/// Whether two forenames are two spellings of one name, or two names.
/// </summary>
/// <remarks>
/// This is the whole difficulty in finding a player recorded twice. Sharing a
/// surname and a first letter means nothing - Jack and Joel Martin are two
/// people, so are Dave and Donna Smith, Mark and Mike Kerslake, Linda and Luke
/// Perry. A rule that pairs them is not a loose rule, it is a wrong one.
/// <para>
/// So nothing here matches on an initial alone. It asks the narrower question:
/// is one of these the other one's short form, a known familiar form of it, or
/// the same name spelled a letter differently?
/// </para>
/// </remarks>
public static class PlayerNames
{
    /// <summary>
    /// Familiar forms, grouped by the name they belong to.
    /// </summary>
    /// <remarks>
    /// Built from the names actually on this league's books rather than from a
    /// general list: Michael/Mick/Mike and Andrew/Andy are the pairs that turn
    /// up, and each one added is a chance to be wrong, so the table earns its
    /// entries rather than collecting them.
    /// </remarks>
    private static readonly string[][] Families =
    {
        new[] { "alexander", "alex", "al", "sandy" },
        new[] { "andrew", "andy", "drew", "and" },
        new[] { "anthony", "tony", "ant" },
        new[] { "benjamin", "ben", "benny" },
        new[] { "bernard", "bernie" },
        new[] { "catherine", "katherine", "kathryn", "cathy", "kathy", "kate", "katie", "cath" },
        new[] { "charles", "charlie", "chas", "chaz" },
        new[] { "christopher", "chris", "kris" },
        new[] { "daniel", "dan", "danny" },
        new[] { "david", "dave", "davey", "davie" },
        new[] { "deborah", "debbie", "deb" },
        new[] { "dominic", "dom" },
        new[] { "donald", "don" },
        new[] { "edward", "ed", "edd", "eddie", "eddy", "ted" },
        new[] { "elizabeth", "liz", "lizzie", "beth", "betty" },
        new[] { "francis", "frank", "frankie" },
        new[] { "frederick", "fred", "freddie" },
        new[] { "gary", "gaz" },
        new[] { "geoffrey", "jeffrey", "geoff", "jeff" },
        new[] { "gerald", "gerry", "jerry" },
        new[] { "gregory", "greg" },
        new[] { "harold", "harry" },
        new[] { "james", "jim", "jimmy", "jamie", "jimbo" },
        new[] { "jennifer", "jenny", "jen" },
        new[] { "joanne", "joanna", "joan", "jo" },
        new[] { "john", "jon", "johnny", "jonny" },
        new[] { "jonathan", "jon", "jonny", "jonathon" },
        new[] { "joseph", "joe", "joey" },
        new[] { "joshua", "josh" },
        new[] { "kenneth", "ken", "kenny" },
        new[] { "lawrence", "laurence", "laurie", "larry" },
        new[] { "leonard", "len", "lenny" },
        new[] { "margaret", "maggie", "peggy", "marge" },
        new[] { "martin", "marty" },
        new[] { "matthew", "matt", "matty" },
        new[] { "maurice", "mo" },
        new[] { "michael", "mike", "mick", "mickey", "micky", "mikey" },
        new[] { "nathan", "nath", "nate" },
        new[] { "nicholas", "nick", "nicky" },
        new[] { "oliver", "olly", "ollie" },
        new[] { "patricia", "pat", "tricia", "trish" },
        new[] { "patrick", "pat", "paddy" },
        new[] { "peter", "pete" },
        new[] { "philip", "phillip", "phil" },
        new[] { "raymond", "ray" },
        new[] { "rebecca", "becky", "becca", "bex" },
        new[] { "richard", "rich", "richie", "rick", "ricky", "dick" },
        new[] { "robert", "rob", "robbie", "bob", "bobby" },
        new[] { "ronald", "ron", "ronnie" },
        new[] { "russell", "russ" },
        new[] { "samuel", "sam", "sammy" },
        new[] { "samantha", "sam", "sammy" },
        new[] { "simon", "si" },
        new[] { "stephen", "steven", "steve", "stevie" },
        new[] { "stuart", "stewart", "stu" },
        new[] { "terence", "terry", "tel" },
        new[] { "thomas", "tom", "tommy" },
        new[] { "timothy", "tim", "timmy" },
        new[] { "victoria", "vicky", "vicki", "vic" },
        new[] { "vincent", "vince", "vinny" },
        new[] { "walter", "wally", "walt" },
        new[] { "william", "will", "bill", "billy", "willy", "liam" },
        new[] { "zachary", "zach", "zak" },
    };

    /// <summary>Forename to the families it belongs to; some belong to two.</summary>
    private static readonly Dictionary<string, HashSet<int>> Membership = Build();

    private static Dictionary<string, HashSet<int>> Build()
    {
        var map = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        for (var i = 0; i < Families.Length; i++)
        {
            foreach (var name in Families[i])
            {
                if (!map.TryGetValue(name, out var families))
                {
                    families = new HashSet<int>();
                    map[name] = families;
                }

                families.Add(i);
            }
        }

        return map;
    }

    /// <summary>
    /// True when two forenames are known forms of one name.
    /// </summary>
    /// <remarks>
    /// "Jon" belongs to both John and Jonathan, so a name in two families
    /// matches anything in either - which is right: whichever of the two he is,
    /// Jon and Johnny are the same person.
    /// </remarks>
    public static bool SameFamily(string left, string right)
    {
        if (left == right) return false;

        return Membership.TryGetValue(left, out var a)
            && Membership.TryGetValue(right, out var b)
            && a.Overlaps(b);
    }

    /// <summary>
    /// True when one forename is the other shortened - "Edd" out of "Eddie".
    /// </summary>
    /// <remarks>
    /// Three letters at least, so "Jo" does not swallow "Joel" and "John". The
    /// two-letter short forms that are real are in the table instead, where
    /// they are named rather than guessed.
    /// </remarks>
    public static bool Shortened(string left, string right)
    {
        if (left == right) return false;

        var (shorter, longer) = left.Length < right.Length ? (left, right) : (right, left);

        return shorter.Length >= 3 && longer.StartsWith(shorter, StringComparison.Ordinal);
    }

    /// <summary>
    /// True when one is a bare initial standing for the other.
    /// </summary>
    /// <remarks>
    /// Only ever used where the surname has exactly one full forename starting
    /// with that letter. "J Smith" where the books hold both a Jamie and a Jez
    /// Smith is not a match waiting to be made, it is a question only the
    /// secretary can answer.
    /// </remarks>
    public static bool IsInitial(string value) =>
        value.Length == 1 || (value.Length == 2 && value[1] == '.');

    /// <summary>The letter a bare initial stands for.</summary>
    public static char Letter(string value) => value[0];

    /// <summary>
    /// True when two names are one letter apart - "Clare" for "Claire".
    /// </summary>
    /// <remarks>
    /// Four letters at least. At three, one letter of difference is most of the
    /// name: Kim and Kit, Jon and Jan, Dan and Don are all different people.
    /// The short names that genuinely vary are in the table.
    /// </remarks>
    public static bool OneLetterApart(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0) return false;
        if (left == right) return false;
        if (Math.Abs(left.Length - right.Length) > 1) return false;
        if (Math.Min(left.Length, right.Length) < 4) return false;

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++) previous[j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length] <= 1;
    }
}
