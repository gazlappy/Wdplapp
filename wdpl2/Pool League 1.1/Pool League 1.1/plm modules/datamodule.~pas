unit datamodule;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  Db, DBTables;

type
  TDM1 = class(TDataModule)
    Player: TTable;
    PlayerPlayerNo: TFloatField;
    PlayerPlayerName: TStringField;
    PlayerSource: TDataSource;
    LastPlayerNoQuery: TQuery;
    Team: TTable;
    TeamSource: TDataSource;
    DeleteQuery: TQuery;
    TeamTeamName: TStringField;
    TeamContact: TStringField;
    TeamContactAddress1: TStringField;
    TeamContactAddress2: TStringField;
    TeamContactAddress3: TStringField;
    TeamContactAddress4: TStringField;
    TeamWins: TIntegerField;
    TeamLoses: TIntegerField;
    TeamDraws: TIntegerField;
    TeamSWins: TIntegerField;
    TeamSLosses: TIntegerField;
    TeamDWins: TIntegerField;
    TeamDLosses: TIntegerField;
    TeamPoints: TIntegerField;
    TeamPlayed: TIntegerField;
    League: TTable;
    LeagueSource: TDataSource;
    LeagueNoSingles: TIntegerField;
    LeagueNoDoubles: TIntegerField;
    LeagueSinglesBonus: TIntegerField;
    LeagueDoublesBonus: TIntegerField;
    LeagueWinBonus: TIntegerField;
    LeagueDrawBonus: TIntegerField;
    LeagueLossBonus: TIntegerField;
    LeagueWinFactor: TFloatField;
    LeagueLossFactor: TFloatField;
    LeagueEightBallFactor: TFloatField;
    LeagueUpdateRequired: TBooleanField;
    LeagueMaxSingles: TIntegerField;
    LeagueMaxDoubles: TIntegerField;
    LeagueBuffer: TIntegerField;
    LeagueLetterhead: TMemoField;
    LeagueShowTop: TIntegerField;
    LeagueLatestFrameWeight: TIntegerField;
    LeagueWeightDrop: TIntegerField;
    LeagueWeightGames: TIntegerField;
    LeagueLeagueName: TStringField;
    LeagueSeason: TStringField;
    LeagueExplanation: TMemoField;
    DblrateQuery: TQuery;
    DblrateQueryPlayerNo1: TFloatField;
    DblrateQueryPlayerNo2: TFloatField;
    Match: TTable;
    MatchSource: TDataSource;
    Single: TTable;
    SingleSource: TDataSource;
    MatchMatchNo: TFloatField;
    MatchMatchDate: TDateField;
    SingleHPN: TStringField;
    HomePlayerLookUp: TTable;
    HomePlayerLookUpPlayerNo: TFloatField;
    HomePlayerLookUpPlayerName: TStringField;
    SingleAPN: TStringField;
    AwayPlayerLookUp: TTable;
    StringField1: TStringField;
    TeamByName: TQuery;
    TeamByNameTeamName: TStringField;
    DoubleSource: TDataSource;
    Double: TTable;
    DoubleMatchNo: TFloatField;
    DoubleDoubleNo: TFloatField;
    DoubleHomePlayerNo1: TFloatField;
    DoubleHomePlayerNo2: TFloatField;
    DoubleAwayPlayerNo1: TFloatField;
    DoubleAwayPlayerNo2: TFloatField;
    DoubleWinner: TStringField;
    DoubleEightBall1: TBooleanField;
    DoubleEightBall2: TBooleanField;
    DoubleHPN1: TStringField;
    DoubleHPN2: TStringField;
    DoubleAPN1: TStringField;
    DoubleAPN2: TStringField;
    DeleteDoubles: TQuery;
    DeleteSingles: TQuery;
    LastMatchNoQuery: TQuery;
    Division: TTable;
    DivisionSource: TDataSource;
    PlayerQuery: TQuery;
    PairQuery: TQuery;
    PairQueryPlayerNo1: TFloatField;
    PairQueryPlayerNo2: TFloatField;
    PairQueryPlayed: TIntegerField;
    PairQueryWins: TIntegerField;
    PairQueryLosses: TIntegerField;
    PairQueryBestRating: TIntegerField;
    PairQueryBestRatingDate: TDateField;
    PairQueryCurrentRating: TIntegerField;
    TeamQuery: TQuery;
    TeamQueryWins: TIntegerField;
    TeamQueryLoses: TIntegerField;
    TeamQueryDraws: TIntegerField;
    TeamQuerySWins: TIntegerField;
    TeamQuerySLosses: TIntegerField;
    TeamQueryDWins: TIntegerField;
    TeamQueryDLosses: TIntegerField;
    TeamQueryPoints: TIntegerField;
    TeamQueryPlayed: TIntegerField;
    TeamQueryDeduction: TIntegerField;
    TeamQueryNett: TIntegerField;
    TeamQueryFinesDue: TCurrencyField;
    PlayerQueryPlayerNo: TFloatField;
    PlayerQueryPlayerName: TStringField;
    PlayerQueryPlayed: TIntegerField;
    PlayerQueryWins: TIntegerField;
    PlayerQueryLosses: TIntegerField;
    PlayerQueryCurrentRating: TIntegerField;
    PlayerQueryBestRating: TIntegerField;
    PlayerQueryBestRatingDate: TDateField;
    PlayerQueryEightBalls: TIntegerField;
    DateRateQuery: TQuery;
    DateRateQueryDateRateKey: TIntegerField;
    DateRateQueryPlayerNo: TFloatField;
    DateRateQueryWon: TBooleanField;
    DateRateQueryAgainst: TFloatField;
    DateRateQueryRating: TIntegerField;
    DateRateQueryRatingDate: TDateField;
    AllPlayerLookUp: TTable;
    AllPlayerLookUpPlayerNo: TFloatField;
    AllPlayerLookUpPlayerName: TStringField;
    AllPlayerLookUpPlayed: TIntegerField;
    AllPlayerLookUpWins: TIntegerField;
    AllPlayerLookUpLosses: TIntegerField;
    AllPlayerLookUpCurrentRating: TIntegerField;
    AllPlayerLookUpBestRating: TIntegerField;
    AllPlayerLookUpBestRatingDate: TDateField;
    AllPlayerLookUpEightBalls: TIntegerField;
    PlayerQueryByName: TQuery;
    PlayerQueryByNamePlayerNo: TFloatField;
    PlayerQueryByNamePlayerName: TStringField;
    PlayerQueryByNamePlayed: TIntegerField;
    PlayerQueryByNameWins: TIntegerField;
    PlayerQueryByNameLosses: TIntegerField;
    PlayerQueryByNameCurrentRating: TIntegerField;
    PlayerQueryByNameBestRating: TIntegerField;
    PlayerQueryByNameBestRatingDate: TDateField;
    PlayerQueryByNameEightBalls: TIntegerField;
    EBQuery: TQuery;
    MatchQuery: TQuery;
    MatchQueryMatchNo: TFloatField;
    MatchQueryMatchDate: TDateField;
    MatchQueryHSWins: TIntegerField;
    MatchQueryASWins: TIntegerField;
    MatchQueryHDWins: TIntegerField;
    MatchQueryADWins: TIntegerField;
    HomeSingleCheck: TQuery;
    HomeSingleCheckMatchNo: TFloatField;
    HomeSingleCheckHomePlayerNo: TFloatField;
    HomeSingleCheckSingleNo: TFloatField;
    Database: TDatabase;
    SingleMatchNo: TFloatField;
    SingleSingleNo: TFloatField;
    SingleHomePlayerNo: TFloatField;
    SingleAwayPlayerNo: TFloatField;
    SingleWinner: TStringField;
    SingleEightBall: TBooleanField;
    AwaySingleCheck: TQuery;
    FloatField2: TFloatField;
    FloatField4: TFloatField;
    AwayDoubleCheck: TQuery;
    Venue: TTable;
    VenueSource: TDataSource;
    Team_1: TTable;
    Team_1Deduction: TIntegerField;
    Team_1AmtFined: TCurrencyField;
    Team_1FinesPaid: TCurrencyField;
    Team_1FinesDue: TCurrencyField;
    Team_1Source: TDataSource;
    AwayDoubleCheckMatchNo: TFloatField;
    AwayDoubleCheckDoubleNo: TFloatField;
    AwayDoubleCheckAwayPlayerNo1: TFloatField;
    AwayDoubleCheckAwayPlayerNo2: TFloatField;
    HomeDoubleCheck: TQuery;
    FloatField5: TFloatField;
    FloatField6: TFloatField;
    HomeSingleCheckHPN: TStringField;
    AwaySingleCheckAwayPlayerNo: TFloatField;
    AwaySingleCheckAPN: TStringField;
    HomeDoubleCheckHomePlayerNo1: TFloatField;
    HomeDoubleCheckHomePlayerNo2: TFloatField;
    HomeDoubleCheckHPN1: TStringField;
    HomeDoubleCheckHPN2: TStringField;
    SingleLookUp: TTable;
    SingleLookUpMatchNo: TFloatField;
    SingleLookUpSingleNo: TFloatField;
    SingleLookUpHomePlayerNo: TFloatField;
    SingleLookUpAwayPlayerNo: TFloatField;
    DoubleLookUp: TTable;
    DoubleLookUpMatchNo: TFloatField;
    DoubleLookUpDoubleNo: TFloatField;
    DoubleLookUpHomePlayerNo1: TFloatField;
    DoubleLookUpHomePlayerNo2: TFloatField;
    DoubleLookUpAwayPlayerNo1: TFloatField;
    DoubleLookUpAwayPlayerNo2: TFloatField;
    AwayPlayerLookUpPlayerNo: TFloatField;
    AwayDoubleCheckAPN1: TStringField;
    AwayDoubleCheckAPN2: TStringField;
    LeagueAdministrator: TMemoField;
    TeamWithdrawn: TBooleanField;
    TeamRemoveResults: TBooleanField;
    TeamQueryWithdrawn: TBooleanField;
    TeamQueryRemoveResults: TBooleanField;
    HPLUS: TDataSource;
    APLUS: TDataSource;
    LeagueSlightly: TBooleanField;
    LeagueModerately: TBooleanField;
    LeagueHighly: TBooleanField;
    LeagueEightBallHigh: TBooleanField;
    LastDivisionNoQuery: TQuery;
    TeamDivision: TIntegerField;
    TeamQueryDivision: TIntegerField;
    DivisionItem_id: TIntegerField;
    DivisionAbbreviated: TStringField;
    DivisionFullDivisionName: TStringField;
    TeamDivisionName: TStringField;
    LastVenueNoQuery: TQuery;
    VenueItem_id: TIntegerField;
    VenueVenue: TStringField;
    VenueAddressLine1: TStringField;
    VenueAddressLine2: TStringField;
    VenueAddressLine3: TStringField;
    VenueAddressLine4: TStringField;
    TeamVenue: TIntegerField;
    TeamVenueName: TStringField;
    TeamItem_id: TIntegerField;
    Team_1Item_id: TIntegerField;
    LastTeamNoQuery: TQuery;
    MatchHomeTeam: TIntegerField;
    MatchAwayTeam: TIntegerField;
    MatchHomeTeamName: TStringField;
    MatchAwayTeamName: TStringField;
    AllPlayerLookUpPlayerTeam: TIntegerField;
    HomePlayerLookUpPlayerTeam: TIntegerField;
    PlayerQueryByNamePlayerTeam: TIntegerField;
    PlayerQueryPlayerTeam: TIntegerField;
    PlayerPlayerTeam: TIntegerField;
    PlayerQueryByNamePlayerTeamName: TStringField;
    AllPlayerLookUpPlayerTeamName: TStringField;
    PlayerQueryPlayerTeamName: TStringField;
    PairQueryPlayerTeam: TIntegerField;
    PairQueryPlayerName1: TStringField;
    PairQueryPlayerName2: TStringField;
    PairQueryPlayerTeamName: TStringField;
    TeamQueryTeamName: TStringField;
    MatchQueryHomeTeam: TIntegerField;
    MatchQueryAwayTeam: TIntegerField;
    MatchQueryHomeTeamName: TStringField;
    MatchQueryAwayTeamName: TStringField;
    MatchQueryDivItem_id: TIntegerField;
    MatchQueryDivName: TStringField;
    AwayPlayerLookUpPlayerTeam: TIntegerField;
    EBQueryPlayerNo: TFloatField;
    EBQueryPlayerName: TStringField;
    EBQueryPlayerTeam: TIntegerField;
    EBQueryEightBalls: TIntegerField;
    EBQueryPlayerTeamName: TStringField;
    Pair: TTable;
    Dbls: TTable;
    TeamByNameItem_id: TIntegerField;
    MatchHomeTeamVenueName: TStringField;
    MatchAwayTeamVenueName: TStringField;
    MatchHSWins: TIntegerField;
    MatchASWins: TIntegerField;
    MatchHDWins: TIntegerField;
    MatchADWins: TIntegerField;
    MatchDivName: TStringField;
    MatchHomeTeamPoints: TIntegerField;
    MatchAwayTeamPoints: TIntegerField;
    procedure PlayerBeforePost(DataSet: TDataSet);
    procedure SingleWinnerValidate(Sender: TField);
    procedure SingleEightBallValidate(Sender: TField);
    procedure SingleNewRecord(DataSet: TDataSet);
    procedure DoubleNewRecord(DataSet: TDataSet);
    procedure MatchNewRecord(DataSet: TDataSet);
    procedure DoubleEightBall1Change(Sender: TField);
    procedure DoubleEightBall1Validate(Sender: TField);
    procedure DoubleWinnerValidate(Sender: TField);
    procedure DoubleEightBall2Change(Sender: TField);
    procedure DoubleEightBall2Validate(Sender: TField);
    procedure MatchAfterRefresh(DataSet: TDataSet);
    procedure SingleBeforeDelete(DataSet: TDataSet);
    procedure DoubleBeforeDelete(DataSet: TDataSet);
    procedure MatchBeforeDelete(DataSet: TDataSet);
    procedure MatchHomeTeamValidate(Sender: TField);
    procedure MatchAwayTeamValidate(Sender: TField);
    procedure MatchHomeTeamChange(Sender: TField);
    procedure RemoveOnTeamChange;
    procedure MatchAwayTeamChange(Sender: TField);
    procedure SingleBeforeInsert(DataSet: TDataSet);
    procedure DoubleBeforeInsert(DataSet: TDataSet);
    procedure DoubleHomePlayerNo1Validate(Sender: TField);
    procedure DoubleHomePlayerNo2Validate(Sender: TField);
    procedure DoubleAwayPlayerNo1Validate(Sender: TField);
    procedure DoubleAwayPlayerNo2Validate(Sender: TField);
    procedure MatchAfterScroll(DataSet: TDataSet);
    procedure SingleHomePlayerNoValidate(Sender: TField);
    procedure SingleAwayPlayerNoValidate(Sender: TField);
    procedure TeamBeforePost(DataSet: TDataSet);
    procedure DoubleBeforePost(DataSet: TDataSet);
    procedure SingleAfterPost(DataSet: TDataSet);
    procedure DoubleAfterPost(DataSet: TDataSet);
    procedure DivisionNewRecord(DataSet: TDataSet);
    procedure VenueNewRecord(DataSet: TDataSet);
    procedure TeamNewRecord(DataSet: TDataSet);
    procedure DivisionAfterRefresh(DataSet: TDataSet);
    procedure MatchCalcFields(DataSet: TDataSet);
    procedure VenueBeforeRefresh(DataSet: TDataSet);
    procedure TeamBeforeRefresh(DataSet: TDataSet);
    procedure AllPlayerLookUpBeforeRefresh(DataSet: TDataSet);
  private
    { Private declarations }
  public
    procedure HomeHowManyFrames(var Failed: Boolean; CheckPlayer: Double);
    procedure AwayHowManyFrames(var Failed: Boolean; CheckPlayer: Double);
    procedure HomeMaxDoublesCheck(var Failed: Boolean; CheckPlayer: Double);
    procedure AwayMaxDoublesCheck(var Failed: Boolean; CheckPlayer: Double);
    { Public declarations }
  end;

var
  DM1: TDM1;

implementation

uses Main, Player;
{used within Player Table so that Singles and Doubles may call
 for player additions}
{$R *.DFM}

procedure TDM1.PlayerBeforePost(DataSet: TDataSet);
begin
  if DM1.Player.State = dsInsert then              { fetch new ItemNo for the key }
  begin
    DM1.LastPlayerNoQuery.Close;
    DM1.LastPlayerNoQuery.Open;
    { SQL servers return Null for some aggregates if no items are present }
    with DM1.LastPlayerNoQuery.Fields[0] do
      if IsNull then DM1.PlayerPlayerNo.Value := 1
      else DM1.PlayerPlayerNo.Value := AsFloat + 1;
  end;
end;

procedure TDM1.SingleWinnerValidate(Sender: TField);
begin
  if (SingleWinner.Value <> 'Home') and (SingleWinner.Value <> 'Away') then
    raise Exception.Create('You must enter `Home` or `Away`');
end;

procedure TDM1.SingleEightBallValidate(Sender: TField);
begin
  if (SingleEightBall.Value <> True) and (SingleEightBall.Value <> False) then
    raise Exception.Create('You must enter `True` or `False`');
end;

procedure TDM1.SingleNewRecord(DataSet: TDataSet);
begin
  SingleSingleNo.Value := Single.RecordCount + 1;
end;

procedure TDM1.DoubleNewRecord(DataSet: TDataSet);
begin
  DoubleDoubleNo.Value := Double.RecordCount + 1;
end;

procedure TDM1.MatchNewRecord(DataSet: TDataSet);
var dummy: Integer;
begin
  DM1.LastMatchNoQuery.Close;
  DM1.LastMatchNoQuery.Open;
  { SQL servers return Null for some aggregates if no items are present }
  with DM1.LastMatchNoQuery.Fields[0] do
    if IsNull then DM1.MatchMatchNo.Value := 1
    else DM1.MatchMatchNo.Value := AsFloat + 1;
  //Create empty singles, need iteration up to no. singles
  dummy := 0;
  while dummy < DM1.LeagueNoSingles.Value do
  begin
    dummy := dummy + 1;
    DM1.Single.Insert;
    DM1.SingleMatchNo.Value := DM1.MatchMatchNo.Value;
    DM1.Single.Post;
  end;
  DM1.Single.Edit;
  DM1.SingleMatchNo.Value := DM1.MatchMatchNo.Value;
  DM1.SingleSingleNo.Value := 1;
  DM1.Single.GotoKey;
//  ... and no. doubles
  dummy := 0;
  while dummy < DM1.LeagueNoDoubles.Value do
  begin
    dummy := dummy + 1;
    DM1.Double.Insert;
    DM1.DoubleMatchNo.Value := DM1.MatchMatchNo.Value;
    DM1.Double.Post;
  end;
  DM1.Match.Post;
end;

procedure TDM1.DoubleEightBall1Change(Sender: TField);
begin
  if DoubleEightBall1.Value = True then
    DoubleEightBall2.Value := False;
end;

procedure TDM1.DoubleEightBall1Validate(Sender: TField);
begin
  if (DoubleEightBall1.Value <> True) and (DoubleEightBall1.Value <> False) then
    raise Exception.Create('You must enter `True` or `False`');
end;

procedure TDM1.DoubleWinnerValidate(Sender: TField);
begin
  if (DoubleWinner.Value <> 'Home') and (DoubleWinner.Value <> 'Away') then
    raise Exception.Create('You must enter `Home` or `Away`');
end;

procedure TDM1.DoubleEightBall2Change(Sender: TField);
begin
  if DoubleEightBall2.Value = True then
    DoubleEightBall1.Value := False;
end;

procedure TDM1.DoubleEightBall2Validate(Sender: TField);
begin
  if (DoubleEightBall2.Value <> True) and (DoubleEightBall2.Value <> False) then
    raise Exception.Create('You must enter `True` or `False`');
end;

procedure TDM1.MatchAfterRefresh(DataSet: TDataSet);
begin
  Form1.Label1.Caption := FloatToStr(Match.RecordCount);
  Form1.Label5.Caption := FloatToStr(Division.RecordCount);
  Form1.Label13.Caption := FloatToStr(Venue.RecordCount);
  Form1.Label15.Caption := FloatToStr(Team.RecordCount);
  Form1.Label17.Caption := FloatToStr(AllPlayerLookUp.RecordCount);
end;

procedure TDM1.SingleBeforeDelete(DataSet: TDataSet);
begin
  ShowMessage('Delete not allowed.');
  Abort;
end;

procedure TDM1.DoubleBeforeDelete(DataSet: TDataSet);
begin
  ShowMessage('Delete not allowed.');
  Abort;
end;

procedure TDM1.MatchBeforeDelete(DataSet: TDataSet);
begin
  if MessageDlg(Format('Delete Match Number "%.0F"?', [MatchMatchNo.Value]),
    mtConfirmation, mbOKCancel, 0) <> mrOK then
    Abort;
  DeleteSingles.Params[0].AsFloat := MatchMatchNo.Value;
  DeleteSingles.Close;
  DeleteSingles.ExecSQL;
  DeleteDoubles.Params[0].AsFloat := MatchMatchNo.Value;
  DeleteDoubles.Close;
  DeleteDoubles.ExecSQL;
end;

procedure TDM1.MatchHomeTeamValidate(Sender: TField);
begin
  if MatchHomeTeam.Value = MatchAwayTeam.Value then
    raise Exception.Create('Home and away teams are the same');
end;

procedure TDM1.MatchAwayTeamValidate(Sender: TField);
begin
  if MatchHomeTeam.Value = MatchAwayTeam.Value then
    raise Exception.Create('Home and away teams are the same');
end;

procedure TDM1.MatchHomeTeamChange(Sender: TField);
begin
  if DM1.MatchHomeTeamName.Value <> '' then RemoveOnTeamChange;
end;

procedure TDM1.MatchAwayTeamChange(Sender: TField);
begin
  if DM1.MatchAwayTeamName.Value <> '' then RemoveOnTeamChange;
end;

procedure TDM1.RemoveOnTeamChange;
begin
  if Match.State = dsInsert then exit;
  if MessageDlg('Changing a team will remove all recorded singles and doubles.  OK to change?',
    mtConfirmation, mbOKCancel, 0) <> mrOK then
    Match.Cancel
  else
  begin
    DeleteSingles.Params[0].AsFloat := MatchMatchNo.Value;
    DeleteSingles.Close;
    DeleteSingles.ExecSQL;
    DeleteDoubles.Params[0].AsFloat := MatchMatchNo.Value;
    DeleteDoubles.Close;
    DeleteDoubles.ExecSQL;
    Single.Refresh;
    Double.Refresh;
  end;
end;

procedure TDM1.SingleBeforeInsert(DataSet: TDataSet);
begin
  if Single.RecordCount >= LeagueNoSingles.Value then
    raise Exception.Create('Maximum number of singles entered');
end;

procedure TDM1.DoubleBeforeInsert(DataSet: TDataSet);
begin
  if Double.RecordCount >= LeagueNoDoubles.Value then
    raise Exception.Create('Maximum number of doubles entered');
end;

procedure TDM1.DoubleHomePlayerNo1Validate(Sender: TField);
var Failed: Boolean;
begin
  if DoubleHomePlayerNo1.Value = DoubleHomePlayerNo2.Value then
    if DoubleHPN2.Value <> 'Void Frame' then
      raise Exception.Create('Home team players are identical.');
  HomeMaxDoublesCheck(Failed, DoubleHomePlayerNo1.Value);
  Form1.DoubleGrid.SelectedIndex := 1;
  if Failed then
    raise Exception.Create('Maximum number of doubles reached for selected player.')
  else
    Form1.DoubleGrid.SelectedIndex := 3;
end;

procedure TDM1.DoubleHomePlayerNo2Validate(Sender: TField);
var Failed: Boolean;
begin
  if DoubleHomePlayerNo1.Value = DoubleHomePlayerNo2.Value then
    if DoubleHPN1.Value <> 'Void Frame' then
      raise Exception.Create('Home team players are identical.');
  HomeMaxDoublesCheck(Failed, DoubleHomePlayerNo2.Value);
  Form1.DoubleGrid.SelectedIndex := 3;
  if Failed then
    raise Exception.Create('Maximum number of doubles reached for selected player.')
  else
    Form1.DoubleGrid.SelectedIndex := 5;
end;

procedure TDM1.HomeMaxDoublesCheck(var Failed: Boolean; CheckPlayer: Double);
var dummy: Integer;
begin
  dummy := 0;
  Failed := False;
  HomeDoubleCheck.Close;
  HomeDoubleCheck.Params[0].AsFloat := CheckPlayer;
  HomeDoubleCheck.Params[1].AsFloat := CheckPlayer;
  HomeDoubleCheck.Params[2].AsFloat := MatchMatchNo.Value;
  HomeDoubleCheck.Params[3].AsFloat := DoubleDoubleNo.Value;
  HomeDoubleCheck.Open;
  HomeDoubleCheck.First;
  while not HomeDoubleCheck.EOF do
  begin
    if HomeDoubleCheckHPN1.Value <> 'Void Frame' then
      if HomeDoubleCheckHPN2.Value <> 'Void Frame' then
        dummy := dummy + 1;
    HomeDoubleCheck.Next;
  end;
  Failed := (dummy >= LeagueMaxDoubles.Value);
end;

procedure TDM1.DoubleAwayPlayerNo1Validate(Sender: TField);
var Failed: Boolean;
begin
  if DoubleAwayPlayerNo1.Value = DoubleAwayPlayerNo2.Value then
    if DoubleAPN2.Value <> 'Void Frame' then
      raise Exception.Create('Away team players are identical.');
  AwayMaxDoublesCheck(Failed, DoubleAwayPlayerNo1.Value);
  Form1.DoubleGrid.SelectedIndex := 5;
  if Failed then
    raise Exception.Create('Maximum number of doubles reached for selected player.')
  else
    Form1.DoubleGrid.SelectedIndex := 7;
end;

procedure TDM1.DoubleAwayPlayerNo2Validate(Sender: TField);
var Failed: Boolean;
begin
  if DoubleAwayPlayerNo1.Value = DoubleAwayPlayerNo2.Value then
    if DoubleAPN1.Value <> 'Void Frame' then
      raise Exception.Create('Away team players are identical.');
  AwayMaxDoublesCheck(Failed, DoubleAwayPlayerNo2.Value);
  Form1.DoubleGrid.SelectedIndex := 7;
  if Failed then
    raise Exception.Create('Maximum number of doubles reached for selected player.')
  else
    Form1.DoubleGrid.SelectedIndex := 9;
end;

procedure TDM1.AwayMaxDoublesCheck(var Failed: Boolean; CheckPlayer: Double);
var dummy: Integer;
begin
  dummy := 0;
  Failed := False;
  AwayDoubleCheck.Close;
  AwayDoubleCheck.Params[0].AsFloat := CheckPlayer;
  AwayDoubleCheck.Params[1].AsFloat := CheckPlayer;
  AwayDoubleCheck.Params[2].AsFloat := MatchMatchNo.Value;
  AwayDoubleCheck.Params[3].AsFloat := DoubleDoubleNo.Value;
  AwayDoubleCheck.Open;
  AwayDoubleCheck.First;
  while not AwayDoubleCheck.EOF do
  begin
    if AwayDoubleCheckAPN1.Value <> 'Void Frame' then
      if AwayDoubleCheckAPN2.Value <> 'Void Frame' then
        dummy := dummy + 1;
    AwayDoubleCheck.Next;
  end;
  Failed := (dummy >= LeagueMaxDoubles.Value);
end;

procedure TDM1.MatchAfterScroll(DataSet: TDataSet);
begin
try
  Single.Refresh;
  Double.Refresh;
  Form1.UpdateDisplayedScore;
except
end;
end;

procedure TDM1.SingleHomePlayerNoValidate(Sender: TField);
var Failed: Boolean;
begin
  Form1.SingleGrid.SelectedIndex := 1;
  HomeHowManyFrames(Failed, SingleHomePlayerNo.Value);
  if Failed then
    raise Exception.Create('Maximum number of singles reached for selected player.')
  else
    Form1.SingleGrid.SelectedIndex := 3;
end;

procedure TDM1.SingleAwayPlayerNoValidate(Sender: TField);
var Failed: Boolean;
begin
  Form1.SingleGrid.SelectedIndex := 3;
  AwayHowManyFrames(Failed, SingleAwayPlayerNo.Value);
  if Failed then
    raise Exception.Create('Maximum number of singles reached for selected player.')
  else
    Form1.SingleGrid.SelectedIndex := 5;
end;

procedure TDM1.HomeHowManyFrames(var Failed: Boolean; CheckPlayer: Double);
var dummy: Integer;
begin
  dummy := 0;
  HomeSingleCheck.Close;
  HomeSingleCheck.Params[0].AsFloat := MatchMatchNo.Value;
  HomeSingleCheck.Params[1].AsFloat := CheckPlayer;
  HomeSingleCheck.Params[2].AsFloat := SingleSingleNo.Value;
  HomeSingleCheck.Open;
  HomeSingleCheck.First;
  while not HomeSingleCheck.EOF do
  begin
// No limit on 'Void Frame'
  if HomeSingleCheckHPN.Value <> 'Void Frame' then
    dummy := dummy + 1;
  HomeSingleCheck.Next;
  end;
  Failed := (dummy >= LeagueMaxSingles.Value) {True};
end;

procedure TDM1.AwayHowManyFrames(var Failed: Boolean; CheckPlayer: Double);
var dummy: Integer;
begin
  dummy := 0;
  Failed := False;
  AwaySingleCheck.Close;
  AwaySingleCheck.Params[0].AsFloat := MatchMatchNo.Value;
  AwaySingleCheck.Params[1].AsFloat := CheckPlayer;
  AwaySingleCheck.Params[2].AsFloat := SingleSingleNo.Value;
  AwaySingleCheck.Open;
  AwaySingleCheck.First;
  while not AwaySingleCheck.EOF do
  begin
// No limit on 'Void Frame'
  if AwaySingleCheckAPN.Value <> 'Void Frame' then
    dummy := dummy + 1;
  AwaySingleCheck.Next;
  end;
  if dummy >= LeagueMaxSingles.Value then
    Failed := True;
end;

procedure TDM1.TeamBeforePost(DataSet: TDataSet);
begin
  if DM1.Team.State = dsInsert then
  begin
    DM1.Player.Open;
    DM1.Player.Insert;
    DM1.PlayerPlayerName.Value := 'Void Frame';
    DM1.PlayerPlayerTeam.Value := DM1.TeamItem_id.Value;
    DM1.Player.Post;
    DM1.Player.Refresh;
  end;
end;

procedure TDM1.DoubleBeforePost(DataSet: TDataSet);
begin
  if (DoubleHPN1.Value = 'Void Frame') and (DoubleHPN2.Value <> 'Void Frame') then
  begin
    DoubleHomePlayerNo2.Value := DoubleHomePlayerNo1.Value;
    MessageDlg('Both players have been set to `Void Frame`', mtWarning, [mbOK], 0);
  end;
  if (DoubleHPN2.Value = 'Void Frame') and (DoubleHPN1.Value <> 'Void Frame') then
  begin
    DoubleHomePlayerNo1.Value := DoubleHomePlayerNo2.Value;
    MessageDlg('Both players have been set to `Void Frame`', mtWarning, [mbOK], 0);
  end;
end;

procedure TDM1.SingleAfterPost(DataSet: TDataSet);
begin
  Form1.UpdateDisplayedScore;
end;

procedure TDM1.DoubleAfterPost(DataSet: TDataSet);
begin
  Form1.UpdateDisplayedScore;
end;

procedure TDM1.DivisionNewRecord(DataSet: TDataSet);
begin
  DM1.LastDivisionNoQuery.Close;
  DM1.LastDivisionNoQuery.Open;
  { SQL servers return Null for some aggregates if no items are present }
  with DM1.LastDivisionNoQuery.Fields[0] do
    if IsNull then DM1.DivisionItem_id.Value := 1
    else DM1.DivisionItem_id.Value := AsInteger + 1;
end;

procedure TDM1.VenueNewRecord(DataSet: TDataSet);
begin
  DM1.LastVenueNoQuery.Close;
  DM1.LastVenueNoQuery.Open;
  { SQL servers return Null for some aggregates if no items are present }
  with DM1.LastVenueNoQuery.Fields[0] do
    if IsNull then DM1.VenueItem_id.Value := 1
    else DM1.VenueItem_id.Value := AsInteger + 1;
end;

procedure TDM1.TeamNewRecord(DataSet: TDataSet);
begin
  DM1.LastTeamNoQuery.Close;
  DM1.LastTeamNoQuery.Open;
  { SQL servers return Null for some aggregates if no items are present }
  with DM1.LastTeamNoQuery.Fields[0] do
    if IsNull then DM1.TeamItem_id.Value := 1
    else DM1.TeamItem_id.Value := AsInteger + 1;
end;

procedure TDM1.DivisionAfterRefresh(DataSet: TDataSet);
begin
  Form1.Label5.Caption := FloatToStr(Division.RecordCount);
end;

procedure TDM1.MatchCalcFields(DataSet: TDataSet);
begin
  MatchAwayTeamPoints.Value := (MatchASWins.Value * LeagueSinglesBonus.Value) + (MatchADWins.Value * LeagueDoublesBonus.Value);
  MatchHomeTeamPoints.Value := (MatchHSWins.Value * LeagueSinglesBonus.Value) + (MatchHDWins.Value * LeagueDoublesBonus.Value);
  if MatchAwayTeamPoints.Value = MatchHomeTeamPoints.Value then
  begin
    MatchAwayTeamPoints.Value := MatchAwayTeamPoints.Value + LeagueDrawBonus.Value;
    MatchHomeTeamPoints.Value := MatchHomeTeamPoints.Value + LeagueDrawBonus.Value;
  end;
  if MatchAwayTeamPoints.Value > MatchHomeTeamPoints.Value then
  begin
    MatchAwayTeamPoints.Value := MatchAwayTeamPoints.Value + LeagueWinBonus.Value;
    MatchHomeTeamPoints.Value := MatchHomeTeamPoints.Value + LeagueLossBonus.Value;
  end;
  if MatchAwayTeamPoints.Value < MatchHomeTeamPoints.Value then
  begin
    MatchAwayTeamPoints.Value := MatchAwayTeamPoints.Value + LeagueLossBonus.Value;
    MatchHomeTeamPoints.Value := MatchHomeTeamPoints.Value + LeagueWinBonus.Value;
  end;
end;

procedure TDM1.VenueBeforeRefresh(DataSet: TDataSet);
begin
  Form1.Label13.Caption := FloatToStr(Venue.RecordCount);
end;

procedure TDM1.TeamBeforeRefresh(DataSet: TDataSet);
begin
  Form1.Label15.Caption := FloatToStr(Team.RecordCount);
end;

procedure TDM1.AllPlayerLookUpBeforeRefresh(DataSet: TDataSet);
begin
  Form1.Label17.Caption := FloatToStr(AllPlayerLookUp.RecordCount);
end;

end.
