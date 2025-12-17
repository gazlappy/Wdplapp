program plm;

uses
  Forms,
  about in '..\plm modules\about.pas' {AboutBox},
  datamodule in '..\plm modules\datamodule.pas' {DM1: TDataModule},
  Main in '..\plm modules\main.pas' {Form1},
  Divins in '..\plm modules\DivIns.pas' {DivisionInsert},
  Division in '..\plm modules\division.pas' {DivisionsForm},
  pickleag in '..\plm modules\pickleag.pas' {PickLeague},
  Player in '..\plm modules\player.pas' {PlayersForm},
  Pnamend in '..\plm modules\pnamend.pas' {PlayerAmend},
  Prop in '..\plm modules\prop.pas' {PropertiesForm},
  Team in '..\plm modules\team.pas' {TeamsForm},
  Teaml in '..\plm modules\teaml.pas' {TeamList},
  Update in '..\plm modules\update.pas' {UpdateForm},
  Venins in '..\plm modules\venins.pas' {VenueInsert},
  Venue in '..\plm modules\venue.pas' {VenuesForm},
  SetDate in '..\plm modules\SetDate.pas' {SelectDt},
  evaluate in '..\plm modules\evaluate.pas' {Form2},
  pickdt in '..\plm modules\pickdt.pas' {PickDate},
  PRating in '..\plm modules\PRating.pas' {RatingReport: TQuickRep},
  PTable in '..\plm modules\PTable.pas' {TableReport: TQuickRep},
  DRatings in '..\plm modules\DRatings.pas' {DoublesReport: TQuickRep},
  plreport in '..\plm modules\plreport.pas' {PlayerReport: TQuickRep},
  eightballrpt in '..\plm modules\eightballrpt.pas' {EightBallReport: TQuickRep},
  resrpt in '..\plm modules\resrpt.pas' {ResultReport: TQuickRep},
  plmmailshot in '..\plm modules\plmmailshot.pas' {Mailshot},
  filterteam in '..\plm modules\filterteam.pas' {FilterDlg},
  covlet in '..\plm modules\covlet.pas' {CovletVenue: TQuickRep},
  covlet2 in '..\plm modules\covlet2.pas' {CovletCaptain: TQuickRep},
  webpages in '..\plm modules\webpages.pas' {WebPageDlg},
  venuelabel in '..\plm modules\venuelabel.pas' {VenueLblRpt: TQuickRep},
  captainlabel in '..\plm modules\captainlabel.pas' {CaptainLblRpt: TQuickRep};

{$R *.RES}

begin
  Application.Initialize;
  Application.Title := 'Admin4Pool';
  Application.HelpFile := '.\Plmhelp.hlp';
  Application.CreateForm(TDM1, DM1);
  Application.CreateForm(TForm1, Form1);
  Application.CreateForm(TRatingReport, RatingReport);
  Application.CreateForm(TTableReport, TableReport);
  Application.CreateForm(TDoublesReport, DoublesReport);
  Application.CreateForm(TPickDate, PickDate);
  Application.CreateForm(TPlayerReport, PlayerReport);
  Application.CreateForm(TEightBallReport, EightBallReport);
  Application.CreateForm(TResultReport, ResultReport);
  Application.CreateForm(TFilterDlg, FilterDlg);
  Application.CreateForm(TCovletVenue, CovletVenue);
  Application.CreateForm(TCovletCaptain, CovletCaptain);
  Application.CreateForm(TDivisionsForm, DivisionsForm);
  Application.CreateForm(TDivisionInsert, DivisionInsert);
  Application.CreateForm(TPickLeague, PickLeague);
  Application.CreateForm(TPlayersForm, PlayersForm);
  Application.CreateForm(TPlayerAmend, PlayerAmend);
  Application.CreateForm(TPropertiesForm, PropertiesForm);
  Application.CreateForm(TTeamsForm, TeamsForm);
  Application.CreateForm(TTeamList, TeamList);
  Application.CreateForm(TUpdateForm, UpdateForm);
  Application.CreateForm(TVenueInsert, VenueInsert);
  Application.CreateForm(TVenuesForm, VenuesForm);
  Application.CreateForm(TSelectDt, SelectDt);
  Application.CreateForm(TForm2, Form2);
  Application.CreateForm(TMailshot, Mailshot);
  Application.CreateForm(TWebPageDlg, WebPageDlg);
  Application.CreateForm(TAboutBox, AboutBox);
  Application.CreateForm(TVenueLblRpt, VenueLblRpt);
  Application.CreateForm(TCaptainLblRpt, CaptainLblRpt);
  Application.Run;
end.
