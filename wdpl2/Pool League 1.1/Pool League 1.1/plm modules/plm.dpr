program Plm;

uses
  Forms,
  Main in 'MAIN.PAS' {Form1},
  Prop in 'PROP.PAS' {PropertiesForm},
  Division in 'DIVISION.PAS' {DivisionsForm},
  Venue in 'VENUE.PAS' {VenuesForm},
  Team in 'TEAM.PAS' {TeamsForm},
  Matchbyt in 'MATCHBYT.PAS' {MatchByTeamForm},
  Update in 'UPDATE.PAS' {UpdateForm},
  Divins in 'DIVINS.PAS' {DivisionInsert},
  Venins in 'VENINS.PAS' {VenueInsert},
  Teaml in 'TEAML.PAS' {TeamList},
  PTable in 'PTable.pas' {TableReport: TQuickRep},
  pickleag in 'pickleag.pas' {PickLeague},
  PRating in 'PRating.pas' {RatingReport: TQuickRep},
  DRatings in 'DRatings.pas' {DoublesReport: TQuickRep},
  resrpt in 'resrpt.pas' {ResultReport: TQuickRep},
  SetDate in 'SetDate.pas' {SelectDt},
  NMatch in 'NMatch.pas' {NMForm},
  Single in 'Single.pas' {Singles},
  Dble in 'Dble.pas' {Doubles},
  Player in 'player.pas' {PlayersForm},
  datamodule in 'datamodule.pas' {DM1: TDataModule},
  about in 'about.pas' {AboutBox},
  mlist in 'mlist.pas' {MtDialog};

{$R *.RES}

begin
  Application.Title := 'Pool League Manager';
  Application.CreateForm(TForm1, Form1);
  Application.CreateForm(TTableReport, TableReport);
  Application.CreateForm(TRatingReport, RatingReport);
  Application.CreateForm(TDoublesReport, DoublesReport);
  Application.CreateForm(TResultReport, ResultReport);
  Application.CreateForm(TDM1, DM1);
  Application.CreateForm(TAboutBox, AboutBox);
  Application.Run;
end.
