namespace Wdpl2.Models
{
    /// <summary>
    /// Type of competition format
    /// </summary>
    public enum CompetitionFormat
    {
        SinglesKnockout,
        DoublesKnockout,
        TeamKnockout,
        RoundRobin,
        Swiss,
        SinglesGroupStage,
        DoublesGroupStage
    }

    /// <summary>
    /// Status of a competition
    /// </summary>
    public enum CompetitionStatus
    {
        Draft,
        InProgress,
        Completed
    }
}
