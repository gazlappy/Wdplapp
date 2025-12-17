unit Match;

interface

uses WinTypes, WinProcs, Classes, Graphics, Forms, Controls, Buttons,
  StdCtrls, Mask, DBCtrls, Grids, DBGrids, DBLookup, DB, DBTables, ExtCtrls,
  Messages, Dialogs, SysUtils;

type
  TMatchesForm = class(TForm)
    OKBtn: TBitBtn;
    HelpBtn: TBitBtn;
    GroupBox1: TGroupBox;
    Label1: TLabel;
    Label2: TLabel;
    DBEdit1: TDBEdit;
    Label3: TLabel;
    Match: TTable;
    MatchSource: TDataSource;
    LastMatchNoQuery: TQuery;
    MatchMatchNo: TFloatField;
    MatchHomeTeam: TStringField;
    MatchAwayTeam: TStringField;
    MatchMatchDate: TDateField;
    HTCombo: TDBLookupCombo;
    Team: TTable;
    TeamSource: TDataSource;
    ATCombo: TDBLookupCombo;
    GroupBox2: TGroupBox;
    SHCombo: TDBComboBox;
    SACombo: TDBComboBox;
    Label4: TLabel;
    Label5: TLabel;
    Single: TTable;
    SingleSource: TDataSource;
    DBNavigator1: TDBNavigator;
    PlayerList: TQuery;
    LastSingle: TQuery;
    SingleMatchNo: TFloatField;
    SingleSingleNo: TFloatField;
    SingleHomePlayerName: TStringField;
    SingleAwayPlayerName: TStringField;
    SingleWinner: TStringField;
    SingleEightBall: TBooleanField;
    GroupBox3: TGroupBox;
    Label8: TLabel;
    Label9: TLabel;
    DH1Combo: TDBComboBox;
    DA1Combo: TDBComboBox;
    DBNavigator2: TDBNavigator;
    DH2Combo: TDBComboBox;
    DA2Combo: TDBComboBox;
    LastDbles: TQuery;
    Double: TTable;
    DoubleSource: TDataSource;
    DoubleMatchNo: TFloatField;
    DoubleDoubleNo: TFloatField;
    DoubleHomePlayerName1: TStringField;
    DoubleHomePlayerName2: TStringField;
    DoubleAwayPlayerName1: TStringField;
    DoubleAwayPlayerName2: TStringField;
    DoubleWinner: TStringField;
    DoubleEightBall: TBooleanField;
    SetUpdate: TQuery;
    SetUpdateUpdateRequired: TSmallintField;
    DBRadioGroup1: TDBRadioGroup;
    DBRadioGroup2: TDBRadioGroup;
    DBRadioGroup3: TDBRadioGroup;
    DBRadioGroup4: TDBRadioGroup;
    LeagueQuery: TQuery;
    LeagueQueryNoSingles: TIntegerField;
    LeagueQueryNoDoubles: TIntegerField;
    Button1: TButton;
    Button2: TButton;
    Button3: TButton;
    Button4: TButton;
    procedure FormCreate(Sender: TObject);
    procedure OKBtnClick(Sender: TObject);
    procedure MatchAfterPost(DataSet: TDataset);
    procedure GroupBox2Enter(Sender: TObject);
    procedure SingleNewRecord(DataSet: TDataset);
    procedure DBNavigator1Click(Sender: TObject; Button: TNavigateBtn);
    procedure MatchNewRecord(DataSet: TDataset);
    procedure ATComboChange(Sender: TObject);
    procedure HTComboChange(Sender: TObject);
    procedure MatchBeforeOpen(DataSet: TDataset);
    procedure GroupBox3Enter(Sender: TObject);
    procedure DoubleNewRecord(DataSet: TDataset);
    procedure MatchHomeTeamChange(Sender: TField);
    procedure MatchAwayTeamChange(Sender: TField);
    procedure DBNavigator2Click(Sender: TObject; Button: TNavigateBtn);
    procedure FormShow(Sender: TObject);
    procedure DH1ComboExit(Sender: TObject);
    procedure DA1ComboExit(Sender: TObject);
    procedure SingleBeforePost(DataSet: TDataSet);
    procedure Button1Click(Sender: TObject);
    procedure Button2Click(Sender: TObject);
    procedure DoubleBeforePost(DataSet: TDataSet);
  private
    { Private declarations }
  public
    { Public declarations }
    procedure Enter;
    procedure Edit(MatchNo: Double);
    procedure AssignAwayPlayers;
    procedure AssignHomePlayers;
    procedure ValidateSinglesNavigation;
    procedure ValidateDoublesNavigation;
  end;

var
  MatchesForm: TMatchesForm;

implementation

uses main, Pnamend;

{$R *.DFM}

procedure TMatchesForm.FormCreate(Sender: TObject);
begin
  Match.Open;
  Team.Open;
  SetUpdate.Close;
  SetUpdate.Open;
  SetUpdate.First;
  SetUpdate.Edit;
  SetUpdateUpdateRequired.Value := 1;
  SetUpdate.Post;
  SetUpdate.Close;
end;

procedure TMatchesForm.Enter;
begin
  Match.Open;
  Match.Insert;
  ShowModal;
end;

procedure TMatchesForm.Edit(MatchNo: Double);
begin
  Match.Open;
  Match.FindKey([MatchNo]);
  Match.Edit;
  Single.Open;
  Single.Edit;
  Double.Open;
  Double.Edit;
  GroupBox1.Caption := 'Match No. ' + MatchMatchNo.AsString;
  AssignAwayPlayers;
  AssignHomePlayers;
  ShowModal;
end;

procedure TMatchesForm.OKBtnClick(Sender: TObject);
begin
  {need to add single and double validation}
  Match.Post;
  GroupBox2.Caption := 'Singles...';
  GroupBox3.Caption := 'Doubles...';
end;

procedure TMatchesForm.MatchAfterPost(DataSet: TDataset);
begin
  ShowMessage('Match has been successfully filed');
  Match.Close;
  Single.Close;
  Close;
end;

procedure TMatchesForm.GroupBox2Enter(Sender: TObject);
begin
  ValidateSinglesNavigation;
end;

procedure TMatchesForm.SingleNewRecord(DataSet: TDataset);
begin
  LastSingle.Close;
  LastSingle.Params[0].AsFloat := MatchMatchNo.Value;
  LastSingle.Open;
  { SQL servers return Null for some aggregates if no items are present }
  with LastSingle.Fields[0] do
    if IsNull then SingleSingleNo.Value := 1
    else SingleSingleNo.Value := AsFloat + 1;
  SingleEightBall.Value := False;
end;


procedure TMatchesForm.DBNavigator1Click(Sender: TObject;
  Button: TNavigateBtn);
begin
  ValidateSinglesNavigation;
end;

procedure TMatchesForm.MatchNewRecord(DataSet: TDataset);
begin
  LastMatchNoQuery.Close;
  LastMatchNoQuery.Open;
  { SQL servers return Null for some aggregates if no items are present }
  with LastMatchNoQuery.Fields[0] do
    if IsNull then MatchMatchNo.Value := 1
    else MatchMatchNo.Value := AsFloat + 1;
GroupBox1.Caption := 'Match No. ' + MatchMatchNo.AsString;

end;

procedure TMatchesForm.ATComboChange(Sender: TObject);
begin
  {AssignAwayPlayers;}
end;

procedure TMatchesForm.HTComboChange(Sender: TObject);
begin
  {AssignHomePlayers; }
end;

procedure TMatchesForm.AssignHomePlayers;
begin
  {Fill combo box of Home Players}
  PlayerList.Close;
  SHCombo.Items.Clear;
  DH1Combo.Items.Clear;
  DH2Combo.Items.Clear;
  PlayerList.Params[0].AsString := MatchHomeTeam.Value;
  PlayerList.Open;
  PlayerList.First;
while not PlayerList.EOF do
begin
  SHCombo.Items.Add(PlayerList.Fields[0].AsString);
  DH1Combo.Items.Add(PlayerList.Fields[0].AsString);
  DH2Combo.Items.Add(PlayerList.Fields[0].AsString);
  PlayerList.Next;
end;
end;

procedure TMatchesForm.ValidateSinglesNavigation;
var invalid: Boolean;
begin
  LastSingle.Close;
  LastSingle.Params[0].AsFloat := MatchMatchNo.Value;
  LastSingle.Open;
  GroupBox2.Caption := 'Single ' + SingleSingleNo.AsString
    + ' of ' + LastSingle.Fields[0].AsString;
  invalid := false;
  if LastSingle.Fields[0].AsFloat = LeagueQueryNoSingles.Value then invalid := true;
  if SingleSingleNo.AsFloat = LeagueQueryNoSingles.Value then invalid := true;
  if invalid then
    DBNavigator1.VisibleButtons := [nbFirst,nbPrior,nbNext,nbLast,nbEdit,nbPost,nbCancel,nbRefresh]
  else
    DBNavigator1.VisibleButtons := [nbFirst,nbPrior,nbNext,nbLast,nbInsert,nbEdit,nbPost,nbCancel,nbRefresh]
end;

procedure TMatchesForm.AssignAwayPlayers;
begin
  {Fill combo box of away players}
  PlayerList.Close;
  SACombo.Items.Clear;
  DA1Combo.Items.Clear;
  DA2Combo.Items.Clear;
  PlayerList.Params[0].AsString := MatchAwayTeam.Value;
  PlayerList.Open;
  PlayerList.First;
while not PlayerList.EOF do
begin
  SACombo.Items.Add(PlayerList.Fields[0].AsString);
  DA1Combo.Items.Add(PlayerList.Fields[0].AsString);
  DA2Combo.Items.Add(PlayerList.Fields[0].AsString);
  PlayerList.Next;
end;
end;


procedure TMatchesForm.MatchBeforeOpen(DataSet: TDataset);
begin
  Single.Open;
  Double.Open;
end;

procedure TMatchesForm.GroupBox3Enter(Sender: TObject);
begin
  ValidateDoublesNavigation;
end;

procedure TMatchesForm.ValidateDoublesNavigation;
var invalid: Boolean;
begin
  LastDbles.Close;
  LastDbles.Params[0].AsFloat := MatchMatchNo.Value;
  LastDbles.Open;
  GroupBox3.Caption := 'Double ' + DoubleDoubleNo.AsString
  + ' of ' + LastDbles.Fields[0].AsString;
  invalid := false;
  if LastDbles.Fields[0].AsFloat = LeagueQueryNoDoubles.Value then invalid := true;
  if DoubleDoubleNo.AsFloat = LeagueQueryNoDoubles.Value then invalid := true;
  if invalid then
    DBNavigator2.VisibleButtons := [nbFirst,nbPrior,nbNext,nbLast,nbEdit,nbPost,nbCancel,nbRefresh]
  else
    DBNavigator2.VisibleButtons := [nbFirst,nbPrior,nbNext,nbLast,nbInsert,nbEdit,nbPost,nbCancel,nbRefresh]
end;

procedure TMatchesForm.DoubleNewRecord(DataSet: TDataset);
begin
  LastDbles.Close;
  LastDbles.Params[0].AsFloat := MatchMatchNo.Value;
  LastDbles.Open;
  { SQL servers return Null for some aggregates if no items are present }
  with LastDbles.Fields[0] do
    if IsNull then DoubleDoubleNo.Value := 1
    else DoubleDoubleNo.Value := AsFloat + 1;
  DoubleEightBall.Value := False;
end;

procedure TMatchesForm.MatchHomeTeamChange(Sender: TField);
begin
  AssignHomePlayers;
end;

procedure TMatchesForm.MatchAwayTeamChange(Sender: TField);
begin
  AssignAwayPlayers;
end;

procedure TMatchesForm.DBNavigator2Click(Sender: TObject;
  Button: TNavigateBtn);
begin
  ValidateDoublesNavigation;
end;

procedure TMatchesForm.FormShow(Sender: TObject);
begin
  Single.Refresh;
  Double.Refresh;
  LeagueQuery.Close;
  LeagueQuery.Open;
  LeagueQuery.First;
  ValidateSinglesNavigation;
  ValidateDoublesNavigation;
end;

procedure TMatchesForm.DH1ComboExit(Sender: TObject);
begin
  if CompareStr(DoubleHomePlayerName1.Value,DoubleHomePlayerName2.Value) = 0 then
  begin
    MessageDlg('You have chosen the same player.  Please choose another.', mtError, [mbOK], 0);
    DH2Combo.SetFocus;
    end;
end;

procedure TMatchesForm.DA1ComboExit(Sender: TObject);
begin
  if CompareStr(DoubleAwayPlayerName1.Value,DoubleAwayPlayerName2.Value) = 0 then
  begin
    MessageDlg('You have chosen the same player.  Please choose another.', mtError, [mbOK], 0);
    DA2Combo.SetFocus;
    end;
end;

procedure TMatchesForm.SingleBeforePost(DataSet: TDataSet);
begin
   if SingleHomePlayerName.Value = '' then
   begin
      MessageDlg('You have not selected a Home Player.', mtError, [mbOK], 0);
      SHCombo.SetFocus;
      Abort;
      end;
   if SingleAwayPlayerName.Value = '' then
   begin
      MessageDlg('You have not selected an Away Player.', mtError, [mbOK], 0);
      SACombo.SetFocus;
      Abort;
      end;
   if SingleWinner.Value = '' then
   begin
      MessageDlg('You have not selected a winner.', mtError, [mbOK], 0);
      DBRadioGroup1.SetFocus;
      Abort;
      end;
end;

procedure TMatchesForm.Button1Click(Sender: TObject);
var sendstring: String;
begin
  sendstring := HTCombo.Value;
  PlayerAmend.Insert(sendstring);
  AssignHomePlayers;
  Single.Refresh;
  Double.Refresh;
end;

procedure TMatchesForm.Button2Click(Sender: TObject);
var sendstring: String;
begin
  sendstring := ATCombo.Value;
  PlayerAmend.Insert(sendstring);
  AssignAwayPlayers;
  Single.Refresh;
  Double.Refresh;
end;

procedure TMatchesForm.DoubleBeforePost(DataSet: TDataSet);
begin
  if DoubleHomePlayerName1.Value = '' then
  begin
    MessageDlg('You have not selected a Home Player.', mtError, [mbOK], 0);
    DH1Combo.SetFocus;
    Abort;
    end;
  if DoubleHomePlayerName2.Value = '' then
  begin
    MessageDlg('You have not selected a Home Player.', mtError, [mbOK], 0);
    DH2Combo.SetFocus;
    Abort;
    end;
  if DoubleAwayPlayerName1.Value = '' then
  begin
    MessageDlg('You have not selected an Away Player.', mtError, [mbOK], 0);
      DA1Combo.SetFocus;
      Abort;
      end;
  if DoubleAwayPlayerName2.Value = '' then
  begin
    MessageDlg('You have not selected an Away Player.', mtError, [mbOK], 0);
      DA2Combo.SetFocus;
      Abort;
      end;
   if DoubleWinner.Value = '' then
   begin
      MessageDlg('You have not selected a winner.', mtError, [mbOK], 0);
      DBRadioGroup3.SetFocus;
      Abort;
      end;
end;

end.
