using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wdpl2.Models
{
    /// <summary>
    /// Represents a competition/tournament with INotifyPropertyChanged for UI updates
    /// </summary>
    public sealed class Competition : INotifyPropertyChanged
    {
        private string _name = "";
        private CompetitionFormat _format = CompetitionFormat.SinglesKnockout;
        private CompetitionStatus _status = CompetitionStatus.Draft;
        private DateTime? _startDate;
        private string? _notes;

        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? SeasonId { get; set; }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public CompetitionFormat Format
        {
            get => _format;
            set
            {
                if (_format != value)
                {
                    _format = value;
                    OnPropertyChanged();
                }
            }
        }

        public CompetitionStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate != value)
                {
                    _startDate = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? Notes
        {
            get => _notes;
            set
            {
                if (_notes != value)
                {
                    _notes = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>List of participant IDs (Players for Singles, Teams for Team KO)</summary>
        public List<Guid> ParticipantIds { get; set; } = new();

        /// <summary>For doubles competitions - pairs of player IDs</summary>
        public List<DoublesTeam> DoublesTeams { get; set; } = new();

        /// <summary>Competition rounds/brackets</summary>
        public List<CompetitionRound> Rounds { get; set; } = new();

        /// <summary>Group stage configuration (for group stage formats)</summary>
        public GroupStageSettings? GroupSettings { get; set; }

        /// <summary>Groups for group stage competitions</summary>
        public List<CompetitionGroup> Groups { get; set; } = new();

        /// <summary>Archived groups from previous group rounds (round 1, round 2, etc.)</summary>
        public List<CompetitionGroup> PreviousGroups { get; set; } = new();

        /// <summary>Best-of frame count for matches (e.g. 15 means first to 8 wins). 0 = unlimited.</summary>
        public int BestOf { get; set; }

        /// <summary>The score needed to win a match. Calculated from BestOf: (BestOf + 1) / 2. Returns 0 if unlimited.</summary>
        public int FramesToWin => BestOf > 0 ? (BestOf + 1) / 2 : 0;

        /// <summary>Whether the draw should be randomised (true) or use the order participants were added (false).</summary>
        public bool RandomDraw { get; set; } = true;

        /// <summary>Linked plate competition ID (for group stage lower-ranked players)</summary>
        public Guid? PlateCompetitionId { get; set; }

        /// <summary>If this competition is a plate/losers cup, the ID of the parent competition that created it.</summary>
        public Guid? ParentCompetitionId { get; set; }

        /// <summary>Participant IDs marked as no-shows. These players are excluded from the plate competition.</summary>
        public List<Guid> NoShowIds { get; set; } = new();

        private bool _isLocked;
        private bool _showOnWebsite = true;

        /// <summary>
        /// When true, this competition is read-only. The editor will block any edits
        /// (participants, draws, scores, settings) until it's unlocked.
        /// </summary>
        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                if (_isLocked != value)
                {
                    _isLocked = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// When false, this competition is hidden from the generated public website
        /// (Competitions page tabs and Home page summaries).
        /// </summary>
        public bool ShowOnWebsite
        {
            get => _showOnWebsite;
            set
            {
                if (_showOnWebsite != value)
                {
                    _showOnWebsite = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString() => Name ?? "Unnamed Competition";
    }
}
