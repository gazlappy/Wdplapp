namespace Wdpl2.Models
{
    /// <summary>
    /// A doubles team (pair of players)
    /// </summary>
    public sealed class DoublesTeam
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid Player1Id { get; set; }
        public Guid Player2Id { get; set; }
        public string TeamName { get; set; } = "";

        public override string ToString() => TeamName;
    }
}
