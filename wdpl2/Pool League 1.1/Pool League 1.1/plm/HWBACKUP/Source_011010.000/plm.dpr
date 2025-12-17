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
  captainlabel in '..\plm modules\captainlabel.pas' {CaptainLblRpt: TQuickRep},
  S_APQS in '..\plm modules\S_APQS.pas' {S_APQSDlg},
  S_HPQS in '..\plm modules\S_HPQS.pas' {S_HPQSDlg},
  D_HPQS in '..\plm modules\D_HPQS.pas' {D_HPQSDlg},
  D_APQS in '..\plm modules\D_APQS.pas' {D_APQSDlg},
  datamodule2 in '..\plm modules\datamodule2.pas' {DM2: TDataModule},
  newleague in '..\plm modules\newleague.pas' {NewLeagueDlg},
  openleague in '..\plm modules\openleague.pas' {OpenLeagueDlg},
  splash in '..\plm modules\splash.pas' {SplashForm},
  backup in '..\plm modules\backup.pas' {BackUpForm};

{$R *.RES}

begin
  Application.Initialize;
// Add splash screen
  SplashForm := TSplashForm.Create(Application);
  SplashForm.Show;
  SplashForm.Update;
  Application.Title := 'Admin4Pool';
  Application.HelpFile := '.\Plmhelp.hlp';
  Application.CreateForm(TDM1, DM1);
  BackUpForm.MassBackUp_To_C;
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
  Application.CreateForm(TMailshot, Mailshot);
  Application.CreateForm(TWebPageDlg, WebPageDlg);
  Application.CreateForm(TAboutBox, AboutBox);
  Application.CreateForm(TVenueLblRpt, VenueLblRpt);
  Application.CreateForm(TCaptainLblRpt, CaptainLblRpt);
  Application.CreateForm(TS_APQSDlg, S_APQSDlg);
  Application.CreateForm(TS_HPQSDlg, S_HPQSDlg);
  Application.CreateForm(TD_HPQSDlg, D_HPQSDlg);
  Application.CreateForm(TD_APQSDlg, D_APQSDlg);
  Application.CreateForm(TDM2, DM2);
  Application.CreateForm(TNewLeagueDlg, NewLeagueDlg);
  Application.CreateForm(TOpenLeagueDlg, OpenLeagueDlg);
  Application.CreateForm(TBackUpForm, BackUpForm);
  SplashForm.Hide;
  SplashForm.Free;
  Application.Run;
end.
