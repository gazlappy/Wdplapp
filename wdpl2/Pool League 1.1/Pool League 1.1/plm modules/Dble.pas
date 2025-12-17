unit Dble;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  Db, DBTables, StdCtrls, Buttons, DBCtrls, Mask, ExtCtrls;

type
  TDoubles = class(TForm)
    SpeedButton1: TSpeedButton;
    SpeedButton2: TSpeedButton;
    ListBox1: TListBox;
    DBEdit1: TDBEdit;
    DBCheckBox1: TDBCheckBox;
    DBEdit2: TDBEdit;
    ListBox2: TListBox;
    DBEdit3: TDBEdit;
    DBEdit4: TDBEdit;
    DBEdit5: TDBEdit;
    DBEdit6: TDBEdit;
    DBEdit7: TDBEdit;
    DBEdit8: TDBEdit;
    DBEdit9: TDBEdit;
    DBEdit10: TDBEdit;
    DBEdit11: TDBEdit;
    DBEdit12: TDBEdit;
    DBEdit13: TDBEdit;
    DBEdit14: TDBEdit;
    DBEdit15: TDBEdit;
    DBEdit16: TDBEdit;
    DBEdit17: TDBEdit;
    DBEdit18: TDBEdit;
    DBEdit19: TDBEdit;
    DBEdit20: TDBEdit;
    DBEdit21: TDBEdit;
    DBEdit22: TDBEdit;
    DBEdit23: TDBEdit;
    DBEdit24: TDBEdit;
    DBCheckBox3: TDBCheckBox;
    DBCheckBox4: TDBCheckBox;
    DBCheckBox5: TDBCheckBox;
    DBCheckBox6: TDBCheckBox;
    BitBtn1: TBitBtn;
    BitBtn2: TBitBtn;
    Double1: TTable;
    DS1: TDataSource;
    PlayerQuery: TQuery;
    PlayerQueryPlayerName: TStringField;
    Double3: TTable;
    DS3: TDataSource;
    Double5: TTable;
    Double2: TTable;
    DS6: TDataSource;
    Double4: TTable;
    DS5: TDataSource;
    Double6: TTable;
    LeagueQuery: TQuery;
    DS2: TDataSource;
    DS4: TDataSource;
    DBCheckBox2: TDBCheckBox;
    Double1MatchNo: TFloatField;
    Double1DoubleNo: TFloatField;
    Double1HomePlayerName1: TStringField;
    Double1HomePlayerName2: TStringField;
    Double1AwayPlayerName1: TStringField;
    Double1AwayPlayerName2: TStringField;
    Double1Winner: TStringField;
    Double3MatchNo: TFloatField;
    Double3DoubleNo: TFloatField;
    Double3HomePlayerName1: TStringField;
    Double3HomePlayerName2: TStringField;
    Double3AwayPlayerName1: TStringField;
    Double3AwayPlayerName2: TStringField;
    Double3Winner: TStringField;
    Double5MatchNo: TFloatField;
    Double5DoubleNo: TFloatField;
    Double5HomePlayerName1: TStringField;
    Double5HomePlayerName2: TStringField;
    Double5AwayPlayerName1: TStringField;
    Double5AwayPlayerName2: TStringField;
    Double5Winner: TStringField;
    Double2MatchNo: TFloatField;
    Double2DoubleNo: TFloatField;
    Double2HomePlayerName1: TStringField;
    Double2HomePlayerName2: TStringField;
    Double2AwayPlayerName1: TStringField;
    Double2AwayPlayerName2: TStringField;
    Double2Winner: TStringField;
    Double4MatchNo: TFloatField;
    Double4DoubleNo: TFloatField;
    Double4HomePlayerName1: TStringField;
    Double4HomePlayerName2: TStringField;
    Double4AwayPlayerName1: TStringField;
    Double4AwayPlayerName2: TStringField;
    Double4Winner: TStringField;
    Double6MatchNo: TFloatField;
    Double6DoubleNo: TFloatField;
    Double6HomePlayerName1: TStringField;
    Double6HomePlayerName2: TStringField;
    Double6AwayPlayerName1: TStringField;
    Double6AwayPlayerName2: TStringField;
    Double6Winner: TStringField;
    LeagueQueryNoDoubles: TIntegerField;
    LeagueQueryMaxDoubles: TIntegerField;
    Double1EightBall1: TBooleanField;
    Double1EightBall2: TBooleanField;
    Double2EightBall1: TBooleanField;
    Double2EightBall2: TBooleanField;
    Double3EightBall1: TBooleanField;
    Double3EightBall2: TBooleanField;
    Double4EightBall1: TBooleanField;
    Double4EightBall2: TBooleanField;
    Double5EightBall1: TBooleanField;
    Double5EightBall2: TBooleanField;
    Double6EightBall1: TBooleanField;
    Double6EightBall2: TBooleanField;
    GroupBox1: TGroupBox;
    RadioButton11: TRadioButton;
    RadioButton12: TRadioButton;
    RadioButton13: TRadioButton;
    GroupBox2: TGroupBox;
    RadioButton21: TRadioButton;
    RadioButton22: TRadioButton;
    RadioButton23: TRadioButton;
    GroupBox3: TGroupBox;
    RadioButton31: TRadioButton;
    RadioButton32: TRadioButton;
    RadioButton33: TRadioButton;
    GroupBox4: TGroupBox;
    RadioButton41: TRadioButton;
    RadioButton42: TRadioButton;
    RadioButton43: TRadioButton;
    GroupBox5: TGroupBox;
    RadioButton51: TRadioButton;
    RadioButton52: TRadioButton;
    RadioButton53: TRadioButton;
    GroupBox6: TGroupBox;
    RadioButton61: TRadioButton;
    RadioButton62: TRadioButton;
    RadioButton63: TRadioButton;
    PQuery: TQuery;
    PQueryPlayerNo: TFloatField;
    Player: TTable;
    PlayerPlayerNo: TFloatField;
    PlayerPlayerName: TStringField;
    PlayerSource: TDataSource;
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
    procedure DBEdit1DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit1DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit1DblClick(Sender: TObject);
    procedure DBEdit3DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit3DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit3DblClick(Sender: TObject);
    procedure DBEdit5DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit5DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit5DblClick(Sender: TObject);
    procedure DBEdit7DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit7DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit7DblClick(Sender: TObject);
    procedure DBEdit9DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit9DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit9DblClick(Sender: TObject);
    procedure DBEdit11DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit11DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit11DblClick(Sender: TObject);
    procedure DBEdit13DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit13DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit13DblClick(Sender: TObject);
    procedure DBEdit15DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit15DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit15DblClick(Sender: TObject);
    procedure DBEdit17DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit17DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit17DblClick(Sender: TObject);
    procedure DBEdit19DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit19DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit19DblClick(Sender: TObject);
    procedure DBEdit21DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit21DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit21DblClick(Sender: TObject);
    procedure DBEdit23DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit23DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit23DblClick(Sender: TObject);
    procedure DBEdit2DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit2DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit2DblClick(Sender: TObject);
    procedure DBEdit4DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit4DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit4DblClick(Sender: TObject);
    procedure DBEdit6DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit6DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit6DblClick(Sender: TObject);
    procedure DBEdit8DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit8DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit8DblClick(Sender: TObject);
    procedure DBEdit10DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit10DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit10DblClick(Sender: TObject);
    procedure DBEdit12DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit12DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit12DblClick(Sender: TObject);
    procedure DBEdit14DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit14DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit14DblClick(Sender: TObject);
    procedure DBEdit16DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit16DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit16DblClick(Sender: TObject);
    procedure DBEdit18DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit18DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit18DblClick(Sender: TObject);
    procedure DBEdit20DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit20DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit20DblClick(Sender: TObject);
    procedure DBEdit22DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit22DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit22DblClick(Sender: TObject);
    procedure DBEdit24DragOver(Sender, Source: TObject; X, Y: Integer;
      State: TDragState; var Accept: Boolean);
    procedure DBEdit24DragDrop(Sender, Source: TObject; X, Y: Integer);
    procedure DBEdit24DblClick(Sender: TObject);
    procedure ListBox1Exit(Sender: TObject);
    procedure ListBox1DblClick(Sender: TObject);
    procedure ListBox2Exit(Sender: TObject);
    procedure ListBox2DblClick(Sender: TObject);
    procedure BitBtn1Click(Sender: TObject);
    procedure SpeedButton1Click(Sender: TObject);
    procedure SpeedButton2Click(Sender: TObject);
    procedure Double1BeforePost(DataSet: TDataSet);
    procedure Double2BeforePost(DataSet: TDataSet);
    procedure Double3BeforePost(DataSet: TDataSet);
    procedure Double4BeforePost(DataSet: TDataSet);
    procedure Double5BeforePost(DataSet: TDataSet);
    procedure Double6BeforePost(DataSet: TDataSet);
    procedure BitBtn2Click(Sender: TObject);
  private
    { Private declarations }
  public
    TMNo: Double;
    hometeam, awayteam: String;
    pno: Double;
    procedure Edit(MatchNo: Double; ATN, HTN: String);
    procedure AssignHomePlayers;
    procedure AssignAwayPlayers;
    procedure RemoveHomePlayer(HPN: String);
    procedure RemoveAwayPlayer(APN: String);
    procedure GetDouble1;
    procedure GetDouble2;
    procedure GetDouble3;
    procedure GetDouble4;
    procedure GetDouble5;
    procedure GetDouble6;
    procedure HideDouble1;
    procedure HideDouble2;
    procedure HideDouble3;
    procedure HideDouble4;
    procedure HideDouble5;
    procedure HideDouble6;
    procedure FormShow;
    function GetPlayerNo(RequiredName, RequiredTeam: String): Double;
    function GetPlayerName(RequiredNo: Double): String;
    function AddToList1(RequiredName: String): Double;
    function RemoveFromList1(RequiredName: String): Double;
    function AddToList2(RequiredName: String): Double;
    function RemoveFromList2(RequiredName: String): Double;
    { Public declarations }
  end;

var
  Doubles: TDoubles;

implementation

uses Main, NMatch, Player;

{$R *.DFM}
function TDoubles.AddToList1(RequiredName: String): Double;
begin
  if RequiredName <> '** Conceeded **' then
    if RequiredName <> 'Unknown' then
      if RequiredName <> '' then
        ListBox1.Items.Add(RequiredName);
end;

function TDoubles.RemoveFromList1(RequiredName: String): Double;
begin
  if RequiredName <> '** Conceeded **' then
    ListBox1.Items.Delete(ListBox1.ItemIndex);
end;

function TDoubles.AddToList2(RequiredName: String): Double;
begin
  if RequiredName <> '** Conceeded **' then
    if RequiredName <> 'Unknown' then
      if RequiredName <> '' then
        ListBox2.Items.Add(RequiredName);
end;

function TDoubles.RemoveFromList2(RequiredName: String): Double;
begin
  if RequiredName <> '** Conceeded **' then
    ListBox2.Items.Delete(ListBox2.ItemIndex);
end;

function TDoubles.GetPlayerNo(RequiredName, RequiredTeam: String): Double;
begin
  if RequiredName = '** Conceeded **' then
  begin
    Result := 0;
  end
  else
  begin
    PQuery.Close;
    PQuery.Params[0].AsString := RequiredName;
    PQuery.Params[1].AsString := RequiredTeam;
    PQuery.Open;
    if PQuery.EOF then
      Result := 0
    else
    begin
      PQuery.First;
      Result := PQueryPlayerNo.Value;
    end;
  end;
end;

function TDoubles.GetPlayerName(RequiredNo: Double): String;
begin
  if RequiredNo = 0 then
  begin
    Result := '** Conceeded **';
  end
  else
  begin
    Player.Open;
    Player.EditKey;
    PlayerPlayerNo.Value := RequiredNo;
    if Player.GotoKey then
      Result := PlayerPlayerName.Value
    else
      Result := 'Unknown';
  end;
end;

procedure TDoubles.Edit(MatchNo: Double; ATN, HTN: String);
begin
  LeagueQuery.Close;
  LeagueQuery.Open;
  LeagueQuery.First;
  if LeagueQueryMaxDoubles.Value = 0 then
  begin
    MessageDlg('Player properties are incomplete.',mtError,[mbOK],0);
    Exit;
  end;
  TMNo := MatchNo;
  hometeam := HTN;
  awayteam := ATN;
  SpeedButton1.Caption := hometeam;
  SpeedButton2.Caption := awayteam;
  AssignHomePlayers;
  AssignAwayPlayers;
  Doubles.Caption := 'Match No. ' + FloatToStr(TMNo) + ' Doubles';
  FormShow;
end;

procedure TDoubles.FormClose(Sender: TObject; var Action: TCloseAction);
begin
  Double1.Close;
  Double2.Close;
  Double3.Close;
  Double4.Close;
  Double5.Close;
  Double6.Close;
  Action := caFree;
  NMForm.SpeedButton2.Enabled := True;
end;

procedure TDoubles.FormShow;
begin
  if LeagueQueryNoDoubles.Value > 0 then
    GetDouble1
  else
    HideDouble1;
  if LeagueQueryNoDoubles.Value > 1 then
    GetDouble2
  else
    HideDouble2;
  if LeagueQueryNoDoubles.Value > 2 then
    GetDouble3
  else
    HideDouble3;
  if LeagueQueryNoDoubles.Value > 3 then
    GetDouble4
  else
    HideDouble4;
  if LeagueQueryNoDoubles.Value > 4 then
    GetDouble5
  else
    HideDouble5;
  if LeagueQueryNoDoubles.Value > 5 then
    GetDouble6
  else
    HideDouble6;
end;

// 6 x Get Double start here
procedure TDoubles.GetDouble1;
begin
  with Double1 do
  begin
    Open;
    EditKey;
    Double1MatchNo.Value := TMNo;
    Double1DoubleNo.Value := 1;
    if GotoKey then
    begin
      Double1.Edit;
      if Double1EightBall1.Value = True then
        RadioButton11.Checked := True
      else
        if Double1EightBall1.Value = True then
          RadioButton12.Checked := True;
      pno := StrToFloat(Double1HomePlayerName1.Value);
      Double1HomePlayerName1.Value := GetPlayerName(pno);
      RemoveHomePlayer(Double1HomePlayerName1.Value);
      pno := StrToFloat(Double1HomePlayerName2.Value);
      Double1HomePlayerName2.Value := GetPlayerName(pno);
      RemoveHomePlayer(Double1HomePlayerName2.Value);
      pno := StrToFloat(Double1AwayPlayerName1.Value);
      Double1AwayPlayerName1.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Double1AwayPlayerName1.Value);
      pno := StrToFloat(Double1AwayPlayerName2.Value);
      Double1AwayPlayerName2.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Double1AwayPlayerName2.Value);
    end
    else
    begin
      Double1.Insert;
      Double1EightBall1.Value := False;
      Double1EightBall2.Value := False;
      Double1Winner.Value := 'Away';
    end;
  end;
end;

procedure TDoubles.GetDouble2;
begin
  with Double2 do
  begin
    Open;
    EditKey;
    Double2MatchNo.Value := TMNo;
    Double2DoubleNo.Value := 2;
    if GotoKey then
    begin
      Double2.Edit;
      if Double2EightBall1.Value = True then
        RadioButton21.Checked := True
      else
        if Double2EightBall1.Value = True then
          RadioButton22.Checked := True;
      pno := StrToFloat(Double2HomePlayerName1.Value);
      Double2HomePlayerName1.Value := GetPlayerName(pno);
      RemoveHomePlayer(Double2HomePlayerName1.Value);
      pno := StrToFloat(Double2HomePlayerName2.Value);
      Double2HomePlayerName2.Value := GetPlayerName(pno);
      RemoveHomePlayer(Double2HomePlayerName2.Value);
      pno := StrToFloat(Double2AwayPlayerName1.Value);
      Double2AwayPlayerName1.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Double2AwayPlayerName1.Value);
      pno := StrToFloat(Double2AwayPlayerName2.Value);
      Double2AwayPlayerName2.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Double2AwayPlayerName2.Value);
    end
    else
    begin
      Double2.Insert;
      Double2EightBall1.Value := False;
      Double2EightBall2.Value := False;
      Double2Winner.Value := 'Away';
    end;
  end;
end;

procedure TDoubles.GetDouble3;
begin
  with Double3 do
  begin
    Open;
    EditKey;
    Double3MatchNo.Value := TMNo;
    Double3DoubleNo.Value := 3;
    if GotoKey then
    begin
      Double3.Edit;
      if Double3EightBall1.Value = True then
        RadioButton31.Checked := True
      else
        if Double3EightBall1.Value = True then
          RadioButton32.Checked := True;
      pno := StrToFloat(Double3HomePlayerName1.Value);
      Double3HomePlayerName1.Value := GetPlayerName(pno);
      RemoveHomePlayer(Double3HomePlayerName1.Value);
      pno := StrToFloat(Double3HomePlayerName2.Value);
      Double3HomePlayerName2.Value := GetPlayerName(pno);
      RemoveHomePlayer(Double3HomePlayerName2.Value);
      pno := StrToFloat(Double3AwayPlayerName1.Value);
      Double3AwayPlayerName1.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Double3AwayPlayerName1.Value);
      pno := StrToFloat(Double3AwayPlayerName2.Value);
      Double3AwayPlayerName2.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Double3AwayPlayerName2.Value);
    end
    else
    begin
      Double3.Insert;
      Double3EightBall1.Value := False;
      Double3EightBall2.Value := False;
      Double3Winner.Value := 'Away';
    end;
  end;
end;

procedure TDoubles.GetDouble4;
begin
  with Double4 do
  begin
    Open;
    EditKey;
    Double4MatchNo.Value := TMNo;
    Double4DoubleNo.Value := 4;
    if GotoKey then
    begin
      Double4.Edit;
      if Double4EightBall1.Value = True then
        RadioButton41.Checked := True
      else
        if Double4EightBall1.Value = True then
          RadioButton42.Checked := True;
      pno := StrToFloat(Double4HomePlayerName1.Value);
      Double4HomePlayerName1.Value := GetPlayerName(pno);
      RemoveHomePlayer(Double4HomePlayerName1.Value);
      pno := StrToFloat(Double4HomePlayerName2.Value);
      Double4HomePlayerName2.Value := GetPlayerName(pno);
      RemoveHomePlayer(Double4HomePlayerName2.Value);
      pno := StrToFloat(Double4AwayPlayerName1.Value);
      Double4AwayPlayerName1.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Double4AwayPlayerName1.Value);
      pno := StrToFloat(Double4AwayPlayerName2.Value);
      Double4AwayPlayerName2.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Double4AwayPlayerName2.Value);
    end
    else
    begin
      Double4.Insert;
      Double4EightBall1.Value := False;
      Double4EightBall2.Value := False;
      Double4Winner.Value := 'Away';
    end;
  end;
end;

procedure TDoubles.GetDouble5;
begin
  with Double5 do
  begin
    Open;
    EditKey;
    Double5MatchNo.Value := TMNo;
    Double5DoubleNo.Value := 5;
    if GotoKey then
    begin
      Double5.Edit;
      if Double5EightBall1.Value = True then
        RadioButton51.Checked := True
      else
        if Double5EightBall1.Value = True then
          RadioButton52.Checked := True;
      pno := StrToFloat(Double5HomePlayerName1.Value);
      Double5HomePlayerName1.Value := GetPlayerName(pno);
      RemoveHomePlayer(Double5HomePlayerName1.Value);
      pno := StrToFloat(Double5HomePlayerName2.Value);
      Double5HomePlayerName2.Value := GetPlayerName(pno);
      RemoveHomePlayer(Double5HomePlayerName2.Value);
      pno := StrToFloat(Double5AwayPlayerName1.Value);
      Double5AwayPlayerName1.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Double5AwayPlayerName1.Value);
      pno := StrToFloat(Double5AwayPlayerName2.Value);
      Double5AwayPlayerName2.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Double5AwayPlayerName2.Value);
    end
    else
    begin
      Double5.Insert;
      Double5EightBall1.Value := False;
      Double5EightBall2.Value := False;
      Double5Winner.Value := 'Away';
    end;
  end;
end;

procedure TDoubles.GetDouble6;
begin
  with Double6 do
  begin
    Open;
    EditKey;
    Double6MatchNo.Value := TMNo;
    Double6DoubleNo.Value := 6;
    if GotoKey then
    begin
      Double6.Edit;
      if Double6EightBall1.Value = True then
        RadioButton61.Checked := True
      else
        if Double6EightBall1.Value = True then
          RadioButton62.Checked := True;
      pno := StrToFloat(Double6HomePlayerName1.Value);
      Double6HomePlayerName1.Value := GetPlayerName(pno);
      RemoveHomePlayer(Double6HomePlayerName1.Value);
      pno := StrToFloat(Double6HomePlayerName2.Value);
      Double6HomePlayerName2.Value := GetPlayerName(pno);
      RemoveHomePlayer(Double6HomePlayerName2.Value);
      pno := StrToFloat(Double6AwayPlayerName1.Value);
      Double6AwayPlayerName1.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Double6AwayPlayerName1.Value);
      pno := StrToFloat(Double6AwayPlayerName2.Value);
      Double6AwayPlayerName2.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Double6AwayPlayerName2.Value);
    end
    else
    begin
      Double6.Insert;
      Double6EightBall1.Value := False;
      Double6EightBall2.Value := False;
      Double6Winner.Value := 'Away';
    end;
  end;
end;

procedure TDoubles.HideDouble1;
begin
  DBEdit1.Visible := False;
  DBEdit2.Visible := False;
  DBCheckBox1.Visible := False;
  GroupBox1.Visible := False;
  RadioButton11.Visible := False;
  RadioButton12.Visible := False;
  RadioButton13.Visible := False;
  DBEdit3.Visible := False;
  DBEdit4.Visible := False;
end;

procedure TDoubles.HideDouble2;
begin
  DBEdit5.Visible := False;
  DBEdit6.Visible := False;
  DBCheckBox2.Visible := False;
  GroupBox2.Visible := False;
  RadioButton21.Visible := False;
  RadioButton22.Visible := False;
  RadioButton23.Visible := False;
  DBEdit7.Visible := False;
  DBEdit8.Visible := False;
end;
procedure TDoubles.HideDouble3;
begin
  DBEdit9.Visible := False;
  DBEdit10.Visible := False;
  DBCheckBox3.Visible := False;
  GroupBox3.Visible := False;
  RadioButton31.Visible := False;
  RadioButton32.Visible := False;
  RadioButton33.Visible := False;
  DBEdit11.Visible := False;
  DBEdit12.Visible := False;
end;
procedure TDoubles.HideDouble4;
begin
  DBEdit13.Visible := False;
  DBEdit14.Visible := False;
  DBCheckBox4.Visible := False;
  GroupBox4.Visible := False;
  RadioButton41.Visible := False;
  RadioButton42.Visible := False;
  RadioButton43.Visible := False;
  DBEdit15.Visible := False;
  DBEdit16.Visible := False;
end;
procedure TDoubles.HideDouble5;
begin
  DBEdit17.Visible := False;
  DBEdit18.Visible := False;
  DBCheckBox5.Visible := False;
  GroupBox5.Visible := False;
  RadioButton51.Visible := False;
  RadioButton52.Visible := False;
  RadioButton53.Visible := False;
  DBEdit19.Visible := False;
  DBEdit20.Visible := False;
end;
procedure TDoubles.HideDouble6;
begin
  DBEdit21.Visible := False;
  DBEdit22.Visible := False;
  DBCheckBox6.Visible := False;
  GroupBox6.Visible := False;
  RadioButton61.Visible := False;
  RadioButton62.Visible := False;
  RadioButton63.Visible := False;
  DBEdit23.Visible := False;
  DBEdit24.Visible := False;
end;

procedure TDoubles.AssignHomePlayers;
//Puts all home players into ListBox1
var i: Integer;
begin
  ListBox1.Items.Clear;
  PlayerQuery.Close;
  PlayerQuery.Params[0].AsString := hometeam;
  PlayerQuery.Open;
  PlayerQuery.First;
  while not PlayerQuery.EOF do
  begin
    for i := 1 to LeagueQueryMaxDoubles.Value do
    begin
    ListBox1.Items.Add(PlayerQueryPlayerName.Value);
    end;
    PlayerQuery.Next;
  end;
  ListBox1.Items.Add('** Conceeded **');
end;

procedure TDoubles.AssignAwayPlayers;
var i: Integer;
//Puts all away players into ListBox2
begin
  ListBox2.Items.Clear;
  PlayerQuery.Close;
  PlayerQuery.Params[0].AsString := awayteam;
  PlayerQuery.Open;
  PlayerQuery.First;
  while not PlayerQuery.EOF do
  begin
    for i := 1 to LeagueQueryMaxDoubles.Value do
    begin
    ListBox2.Items.Add(PlayerQueryPlayerName.Value);
    end;
    PlayerQuery.Next;
  end;
  ListBox2.Items.Add('** Conceeded **');
end;

procedure TDoubles.RemoveAwayPlayer(APN: String);
var i: integer;
begin
//Take out first instance only of Away Player
  if APN = '** Conceeded **' then
    exit;
  i := 0;
  while i <= ListBox2.Items.Count - 1 do
    if ListBox2.Items.Strings[i] = APN then
    begin
      ListBox2.Items.Delete(i);
      i := ListBox2.Items.Count;
    end
    else
    i := i + 1;
end;

procedure TDoubles.RemoveHomePlayer(HPN: String);
var i: integer;
begin
//Take out first instance only of Home Player
  if HPN = '** Conceeded **' then
    exit;
  i := 0;
  while i <= ListBox1.Items.Count - 1 do
    if ListBox1.Items.Strings[i] = HPN then
    begin
      ListBox1.Items.Delete(i);
      i := ListBox1.Items.Count;
    end
    else
    i := i + 1;
end;

//Away Player Drag and Drop starts here
//Double 1, Player 1
procedure TDoubles.DBEdit2DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    if DBEdit4.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      Accept := True
    else
      if DBEdit4.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit2DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit2.Text);
  DBEdit2.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Double1AwayPlayerName1.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit2.Text);
end;
procedure TDoubles.DBEdit2DblClick(Sender: TObject);
begin
  AddToList2(DBEdit2.Text);
  DBEdit2.Text := '';
  Double1AwayPlayerName1.Value := '';
end;
// Double 1, Player 2
procedure TDoubles.DBEdit4DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    if DBEdit2.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      Accept := True
    else
      if DBEdit2.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit4DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit4.Text);
  DBEdit4.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Double1AwayPlayerName2.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit4.Text);
end;
procedure TDoubles.DBEdit4DblClick(Sender: TObject);
begin
  AddToList2(DBEdit4.Text);
  DBEdit4.Text := '';
  Double1AwayPlayerName2.Value := '';
end;
//Double 2, Player 1
procedure TDoubles.DBEdit6DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    if DBEdit8.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      Accept := True
    else
      if DBEdit8.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit6DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit6.Text);
  DBEdit6.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Double2AwayPlayerName1.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit6.Text);
end;
procedure TDoubles.DBEdit6DblClick(Sender: TObject);
begin
  AddToList2(DBEdit6.Text);
  DBEdit6.Text := '';
  Double2AwayPlayerName1.Value := '';
end;
// Double 2, Player 2
procedure TDoubles.DBEdit8DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    if DBEdit6.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      Accept := True
    else
      if DBEdit6.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit8DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit8.Text);
  DBEdit8.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Double2AwayPlayerName2.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit8.Text);
end;
procedure TDoubles.DBEdit8DblClick(Sender: TObject);
begin
  AddToList2(DBEdit8.Text);
  DBEdit8.Text := '';
  Double2AwayPlayerName2.Value := '';
end;
//Double 3, Player 1
procedure TDoubles.DBEdit10DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    if DBEdit12.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      Accept := True
    else
      if DBEdit12.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit10DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit10.Text);
  DBEdit10.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Double3AwayPlayerName1.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit10.Text);
end;
procedure TDoubles.DBEdit10DblClick(Sender: TObject);
begin
  AddToList2(DBEdit10.Text);
  DBEdit10.Text := '';
  Double3AwayPlayerName1.Value := '';
end;
// Double 3, Player 2
procedure TDoubles.DBEdit12DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    if DBEdit10.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      Accept := True
    else
      if DBEdit10.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit12DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit12.Text);
  DBEdit12.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Double3AwayPlayerName2.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit12.Text);
end;
procedure TDoubles.DBEdit12DblClick(Sender: TObject);
begin
  AddToList2(DBEdit12.Text);
  DBEdit12.Text := '';
  Double3AwayPlayerName2.Value := '';
end;
//Double 4, Player 1
procedure TDoubles.DBEdit14DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    if DBEdit16.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      Accept := True
    else
      if DBEdit16.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit14DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit14.Text);
  DBEdit14.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Double4AwayPlayerName1.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit14.Text);
end;
procedure TDoubles.DBEdit14DblClick(Sender: TObject);
begin
  AddToList2(DBEdit14.Text);
  DBEdit14.Text := '';
  Double4AwayPlayerName1.Value := '';
end;
// Double 4, Player 2
procedure TDoubles.DBEdit16DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    if DBEdit14.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      Accept := True
    else
      if DBEdit14.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit16DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit16.Text);
  DBEdit16.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Double4AwayPlayerName2.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit16.Text);
end;
procedure TDoubles.DBEdit16DblClick(Sender: TObject);
begin
  AddToList2(DBEdit16.Text);
  DBEdit16.Text := '';
  Double4AwayPlayerName2.Value := '';
end;
//Double 5, Player 1
procedure TDoubles.DBEdit18DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    if DBEdit20.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      Accept := True
    else
      if DBEdit20.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit18DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit18.Text);
  DBEdit18.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Double5AwayPlayerName1.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit18.Text);
end;
procedure TDoubles.DBEdit18DblClick(Sender: TObject);
begin
  AddToList2(DBEdit18.Text);
  DBEdit18.Text := '';
  Double5AwayPlayerName1.Value := '';
end;
// Double 5, Player 2
procedure TDoubles.DBEdit20DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    if DBEdit18.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      Accept := True
    else
      if DBEdit18.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit20DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit20.Text);
  DBEdit20.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Double5AwayPlayerName2.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit20.Text);
end;
procedure TDoubles.DBEdit20DblClick(Sender: TObject);
begin
  AddToList2(DBEdit20.Text);
  DBEdit20.Text := '';
  Double5AwayPlayerName2.Value := '';
end;
//Double 6, Player 1
procedure TDoubles.DBEdit22DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    if DBEdit24.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      Accept := True
    else
      if DBEdit24.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit22DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit22.Text);
  DBEdit22.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Double6AwayPlayerName1.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit22.Text);
end;
procedure TDoubles.DBEdit22DblClick(Sender: TObject);
begin
  AddToList2(DBEdit22.Text);
  DBEdit22.Text := '';
  Double6AwayPlayerName1.Value := '';
end;
// Double 6, Player 2
procedure TDoubles.DBEdit24DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    if DBEdit22.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      Accept := True
    else
      if DBEdit22.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit24DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit24.Text);
  DBEdit24.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Double6AwayPlayerName2.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit24.Text);
end;
procedure TDoubles.DBEdit24DblClick(Sender: TObject);
begin
  AddToList2(DBEdit24.Text);
  DBEdit24.Text := '';
  Double6AwayPlayerName2.Value := '';
end;

//Home Player Drag and Drop starts here
//Double 1, Player 1
procedure TDoubles.DBEdit1DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    if DBEdit3.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      Accept := True
    else
      if DBEdit3.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit1DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit1.Text);
  DBEdit1.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Double1HomePlayerName1.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit1.Text);
end;
procedure TDoubles.DBEdit1DblClick(Sender: TObject);
begin
  AddToList1(DBEdit1.Text);
  DBEdit1.Text := '';
  Double1HomePlayerName1.Value := '';
end;
// Double 1, Player 2
procedure TDoubles.DBEdit3DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    if DBEdit1.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      Accept := True
    else
      if DBEdit1.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit3DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit3.Text);
  DBEdit3.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Double1HomePlayerName2.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit3.Text);
end;
procedure TDoubles.DBEdit3DblClick(Sender: TObject);
begin
  AddToList1(DBEdit3.Text);
  DBEdit3.Text := '';
  Double1HomePlayerName2.Value := '';
end;
//Double 2, Player 1
procedure TDoubles.DBEdit5DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    if DBEdit7.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      Accept := True
    else
      if DBEdit7.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit5DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit5.Text);
  DBEdit5.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Double2HomePlayerName1.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit5.Text);
end;
procedure TDoubles.DBEdit5DblClick(Sender: TObject);
begin
  AddToList1(DBEdit5.Text);
  DBEdit5.Text := '';
  Double2HomePlayerName1.Value := '';
end;
// Double 2, Player 2
procedure TDoubles.DBEdit7DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    if DBEdit5.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      Accept := True
    else
      if DBEdit5.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit7DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit7.Text);
  DBEdit7.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Double2HomePlayerName2.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit7.Text);
end;
procedure TDoubles.DBEdit7DblClick(Sender: TObject);
begin
  AddToList1(DBEdit7.Text);
  DBEdit7.Text := '';
  Double2HomePlayerName2.Value := '';
end;
//Double 3, Player 1
procedure TDoubles.DBEdit9DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    if DBEdit11.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      Accept := True
    else
      if DBEdit11.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit9DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit9.Text);
  DBEdit9.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Double3HomePlayerName1.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit9.Text);
end;
procedure TDoubles.DBEdit9DblClick(Sender: TObject);
begin
  AddToList1(DBEdit9.Text);
  DBEdit9.Text := '';
  Double3HomePlayerName1.Value := '';
end;
// Double 3, Player 2
procedure TDoubles.DBEdit11DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    if DBEdit9.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      Accept := True
    else
      if DBEdit9.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit11DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit11.Text);
  DBEdit11.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Double3HomePlayerName2.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit11.Text);
end;
procedure TDoubles.DBEdit11DblClick(Sender: TObject);
begin
  AddToList1(DBEdit11.Text);
  DBEdit11.Text := '';
  Double3HomePlayerName2.Value := '';
end;
//Double 4, Player 1
procedure TDoubles.DBEdit13DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    if DBEdit15.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      Accept := True
    else
      if DBEdit15.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit13DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit13.Text);
  DBEdit13.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Double4HomePlayerName1.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit13.Text);
end;
procedure TDoubles.DBEdit13DblClick(Sender: TObject);
begin
  AddToList1(DBEdit13.Text);
  DBEdit13.Text := '';
  Double4HomePlayerName1.Value := '';
end;
// Double 4, Player 2
procedure TDoubles.DBEdit15DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    if DBEdit13.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      Accept := True
    else
      if DBEdit13.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit15DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit15.Text);
  DBEdit15.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Double4HomePlayerName2.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit15.Text);
end;
procedure TDoubles.DBEdit15DblClick(Sender: TObject);
begin
  AddToList1(DBEdit15.Text);
  DBEdit15.Text := '';
  Double4HomePlayerName2.Value := '';
end;
//Double 5, Player 1
procedure TDoubles.DBEdit17DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    if DBEdit19.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      Accept := True
    else
      if DBEdit19.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit17DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit17.Text);
  DBEdit17.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Double5HomePlayerName1.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit17.Text);
end;
procedure TDoubles.DBEdit17DblClick(Sender: TObject);
begin
  AddToList1(DBEdit17.Text);
  DBEdit17.Text := '';
  Double5HomePlayerName1.Value := '';
end;
// Double 5, Player 2
procedure TDoubles.DBEdit19DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    if DBEdit17.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      Accept := True
    else
      if DBEdit17.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit19DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit19.Text);
  DBEdit19.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Double5HomePlayerName2.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit19.Text);
end;
procedure TDoubles.DBEdit19DblClick(Sender: TObject);
begin
  AddToList1(DBEdit19.Text);
  DBEdit19.Text := '';
  Double5HomePlayerName2.Value := '';
end;
//Double 6, Player 1
procedure TDoubles.DBEdit21DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    if DBEdit23.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      Accept := True
    else
      if DBEdit23.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit21DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit21.Text);
  DBEdit21.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Double6HomePlayerName1.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit21.Text);
end;
procedure TDoubles.DBEdit21DblClick(Sender: TObject);
begin
  AddToList1(DBEdit21.Text);
  DBEdit21.Text := '';
  Double6HomePlayerName1.Value := '';
end;
// Double 6, Player 2
procedure TDoubles.DBEdit23DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    if DBEdit21.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      Accept := True
    else
      if DBEdit21.Text = '** Conceeded **' then
        Accept := True;
end;
procedure TDoubles.DBEdit23DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit23.Text);
  DBEdit23.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Double6HomePlayerName2.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit23.Text);
end;
procedure TDoubles.DBEdit23DblClick(Sender: TObject);
begin
  AddToList1(DBEdit23.Text);
  DBEdit23.Text := '';
  Double6HomePlayerName2.Value := '';
end;

// List Box 1 Procedure Start Here
procedure TDoubles.ListBox1Exit(Sender: TObject);
begin
ListBox1.ItemIndex := -1;
end;

procedure TDoubles.ListBox1DblClick(Sender: TObject);
var ok: Boolean;
begin
  ok := false;
  if LeagueQueryNoDoubles.Value < 1 then abort;
  if DBEdit1.Text = '' then
    if DBEdit3.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      ok := true
    else
      if ListBox1.Items.Strings[ListBox1.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
      begin
      DBEdit1.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
      Double1HomePlayerName1.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
      RemoveFromList1(DBEdit1.Text);
      Abort;
  end;
  if DBEdit3.Text = '' then
    if DBEdit1.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      ok := true
    else
      if ListBox1.Items.Strings[ListBox1.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit3.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
      Double1HomePlayerName2.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
      RemoveFromList1(DBEdit3.Text);
      Abort;
  end;
  if LeagueQueryNoDoubles.Value < 2 then abort;
  if DBEdit5.Text = '' then
    if DBEdit7.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      ok := true
    else
      if ListBox1.Items.Strings[ListBox1.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit5.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
      Double2HomePlayerName1.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
      RemoveFromList1(DBEdit5.Text);
      Abort;
  end;
  if DBEdit7.Text = '' then
    if DBEdit5.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      ok := true
    else
      if ListBox1.Items.Strings[ListBox1.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit7.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
      Double2HomePlayerName2.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
      RemoveFromList1(DBEdit7.Text);
      Abort;
  end;
  if LeagueQueryNoDoubles.Value < 3 then abort;
  if DBEdit9.Text = '' then
    if DBEdit11.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      ok := true
    else
      if ListBox1.Items.Strings[ListBox1.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit9.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
      Double3HomePlayerName1.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
      RemoveFromList1(DBEdit9.Text);
      Abort;
  end;
  if DBEdit11.Text = '' then
    if DBEdit9.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      ok := true
    else
      if ListBox1.Items.Strings[ListBox1.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit11.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
      Double3HomePlayerName2.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
      RemoveFromList1(DBEdit11.Text);
      Abort;
  end;
  if LeagueQueryNoDoubles.Value < 4 then abort;
  if DBEdit13.Text = '' then
    if DBEdit15.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      ok := true
    else
      if ListBox1.Items.Strings[ListBox1.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit13.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
      Double4HomePlayerName1.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
      RemoveFromList1(DBEdit13.Text);
      Abort;
  end;
  if DBEdit15.Text = '' then
    if DBEdit13.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      ok := true
    else
      if ListBox1.Items.Strings[ListBox1.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit15.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
      Double4HomePlayerName2.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
      RemoveFromList1(DBEdit15.Text);
      Abort;
  end;
  if LeagueQueryNoDoubles.Value < 5 then abort;
  if DBEdit17.Text = '' then
    if DBEdit19.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      ok := true
    else
      if ListBox1.Items.Strings[ListBox1.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit17.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
      Double5HomePlayerName1.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
      RemoveFromList1(DBEdit17.Text);
      Abort;
  end;
  if DBEdit19.Text = '' then
    if DBEdit17.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      ok := true
    else
      if ListBox1.Items.Strings[ListBox1.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit19.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
      Double5HomePlayerName2.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
      RemoveFromList1(DBEdit19.Text);
      Abort;
  end;
  if LeagueQueryNoDoubles.Value < 6 then abort;
  if DBEdit21.Text = '' then
    if DBEdit23.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      ok := true
    else
      if ListBox1.Items.Strings[ListBox1.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit21.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
      Double6HomePlayerName1.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
      RemoveFromList1(DBEdit21.Text);
      Abort;
  end;
  if DBEdit23.Text = '' then
    if DBEdit21.Text <> ListBox1.Items.Strings[ListBox1.ItemIndex] then
      ok := true
    else
      if ListBox1.Items.Strings[ListBox1.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit23.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
      Double6HomePlayerName2.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
      RemoveFromList1(DBEdit23.Text);
      Abort;
  end;
end;

// List Box 2 Procedure Start Here
procedure TDoubles.ListBox2Exit(Sender: TObject);
begin
ListBox2.ItemIndex := -1;
end;

procedure TDoubles.ListBox2DblClick(Sender: TObject);
var ok: Boolean;
begin
  ok := false;
  if LeagueQueryNoDoubles.Value < 1 then abort;
  if DBEdit2.Text = '' then
    if DBEdit4.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      ok := true
    else
      if ListBox2.Items.Strings[ListBox2.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit2.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
      Double1AwayPlayerName1.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
      RemoveFromList2(DBEdit2.Text);
      Abort;
  end;
  if DBEdit4.Text = '' then
    if DBEdit2.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      ok := true
    else
      if ListBox2.Items.Strings[ListBox2.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit4.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
      Double1AwayPlayerName2.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
      RemoveFromList2(DBEdit4.Text);
      Abort;
  end;
  if LeagueQueryNoDoubles.Value < 2 then abort;
  if DBEdit6.Text = '' then
    if DBEdit8.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      ok := true
    else
      if ListBox2.Items.Strings[ListBox2.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit6.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
      Double2AwayPlayerName1.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
      RemoveFromList2(DBEdit6.Text);
      Abort;
  end;
  if DBEdit8.Text = '' then
    if DBEdit6.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      ok := true
    else
      if ListBox2.Items.Strings[ListBox2.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit8.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
      Double2AwayPlayerName2.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
      RemoveFromList2(DBEdit8.Text);
      Abort;
  end;
  if LeagueQueryNoDoubles.Value < 3 then abort;
  if DBEdit10.Text = '' then
    if DBEdit12.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      ok := true
    else
      if ListBox2.Items.Strings[ListBox2.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit10.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
      Double3AwayPlayerName1.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
      RemoveFromList2(DBEdit10.Text);
      Abort;
  end;
  if DBEdit12.Text = '' then
    if DBEdit10.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      ok := true
    else
      if ListBox2.Items.Strings[ListBox2.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit12.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
      Double3AwayPlayerName2.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
      RemoveFromList2(DBEdit12.Text);
      Abort;
  end;
  if LeagueQueryNoDoubles.Value < 4 then abort;
  if DBEdit14.Text = '' then
    if DBEdit16.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      ok := true
    else
      if ListBox2.Items.Strings[ListBox2.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit14.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
      Double4AwayPlayerName1.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
      RemoveFromList2(DBEdit14.Text);
      Abort;
  end;
  if DBEdit16.Text = '' then
    if DBEdit14.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      ok := true
    else
      if ListBox2.Items.Strings[ListBox2.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit16.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
      Double4AwayPlayerName2.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
      RemoveFromList2(DBEdit16.Text);
      Abort;
  end;
  if LeagueQueryNoDoubles.Value < 5 then abort;
  if DBEdit18.Text = '' then
    if DBEdit20.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      ok := true
    else
      if ListBox2.Items.Strings[ListBox2.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit18.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
      Double5AwayPlayerName1.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
      RemoveFromList2(DBEdit18.Text);
      Abort;
  end;
  if DBEdit20.Text = '' then
    if DBEdit18.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      ok := true
    else
      if ListBox2.Items.Strings[ListBox2.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit20.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
      Double5AwayPlayerName2.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
      RemoveFromList2(DBEdit20.Text);
      Abort;
  end;
  if LeagueQueryNoDoubles.Value < 6 then abort;
  if DBEdit22.Text = '' then
    if DBEdit24.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      ok := true
    else
      if ListBox2.Items.Strings[ListBox2.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit22.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
      Double6AwayPlayerName1.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
      RemoveFromList2(DBEdit22.Text);
      Abort;
  end;
  if DBEdit24.Text = '' then
    if DBEdit22.Text <> ListBox2.Items.Strings[ListBox2.ItemIndex] then
      ok := true
    else
      if ListBox2.Items.Strings[ListBox2.ItemIndex] = '** Conceeded **' then
        ok := true;
    if ok then
    begin
      DBEdit24.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
      Double6AwayPlayerName2.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
      RemoveFromList2(DBEdit24.Text);
      Abort;
  end;
end;

procedure TDoubles.BitBtn1Click(Sender: TObject);
begin
  if LeagueQueryNoDoubles.Value > 0 then
    Double1.Post;
  if LeagueQueryNoDoubles.Value > 1 then
    Double2.Post;
  if LeagueQueryNoDoubles.Value > 2 then
    Double3.Post;
  if LeagueQueryNoDoubles.Value > 3 then
    Double4.Post;
  if LeagueQueryNoDoubles.Value > 4 then
    Double5.Post;
  if LeagueQueryNoDoubles.Value > 5 then
    Double6.Post;
  Close;
end;

procedure TDoubles.SpeedButton1Click(Sender: TObject);
var sendstring: String;
var i: Integer;
begin
  sendstring := hometeam;
  PlayersForm.InsertNewPlayer(sendstring);
  if sendstring = '' then abort;
  for i := 1 to LeagueQueryMaxDoubles.Value do
    ListBox1.Items.Add(sendstring);
end;

procedure TDoubles.SpeedButton2Click(Sender: TObject);
var sendstring: String;
var i: Integer;
begin
  sendstring := awayteam;
  PlayersForm.InsertNewPlayer(sendstring);
  if sendstring = '' then abort;
  for i := 1 to LeagueQueryMaxDoubles.Value do
    ListBox2.Items.Add(sendstring);
end;

// Before Postings Start Here
procedure TDoubles.Double1BeforePost(DataSet: TDataSet);
begin
  Double1MatchNo.Value := TMNo;
  Double1DoubleNo.Value := 1;
  Double1EightBall1.Value := RadioButton11.Checked;
  Double1EightBall2.Value := RadioButton12.Checked;
  pno := GetPlayerNo(Double1HomePlayerName1.Value,hometeam);
  Double1HomePlayerName1.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double1HomePlayerName2.Value,hometeam);
  Double1HomePlayerName2.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double1AwayPlayerName1.Value,awayteam);
  Double1AwayPlayerName1.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double1AwayPlayerName2.Value,awayteam);
  Double1AwayPlayerName2.Value := FloatToStr(pno);
end;
procedure TDoubles.Double2BeforePost(DataSet: TDataSet);
begin
  Double2MatchNo.Value := TMNo;
  Double2DoubleNo.Value := 2;
  Double2EightBall1.Value := RadioButton21.Checked;
  Double2EightBall2.Value := RadioButton22.Checked;
  pno := GetPlayerNo(Double2HomePlayerName1.Value,hometeam);
  Double2HomePlayerName1.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double2HomePlayerName2.Value,hometeam);
  Double2HomePlayerName2.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double2AwayPlayerName1.Value,awayteam);
  Double2AwayPlayerName1.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double2AwayPlayerName2.Value,awayteam);
  Double2AwayPlayerName2.Value := FloatToStr(pno);
end;
procedure TDoubles.Double3BeforePost(DataSet: TDataSet);
begin
  Double3MatchNo.Value := TMNo;
  Double3DoubleNo.Value := 3;
  Double3EightBall1.Value := RadioButton31.Checked;
  Double3EightBall2.Value := RadioButton32.Checked;
  pno := GetPlayerNo(Double3HomePlayerName1.Value,hometeam);
  Double3HomePlayerName1.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double3HomePlayerName2.Value,hometeam);
  Double3HomePlayerName2.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double3AwayPlayerName1.Value,awayteam);
  Double3AwayPlayerName1.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double3AwayPlayerName2.Value,awayteam);
  Double3AwayPlayerName2.Value := FloatToStr(pno);
end;
procedure TDoubles.Double4BeforePost(DataSet: TDataSet);
begin
  Double4MatchNo.Value := TMNo;
  Double4DoubleNo.Value := 4;
  Double4EightBall1.Value := RadioButton41.Checked;
  Double4EightBall2.Value := RadioButton42.Checked;
  pno := GetPlayerNo(Double4HomePlayerName1.Value,hometeam);
  Double4HomePlayerName1.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double4HomePlayerName2.Value,hometeam);
  Double4HomePlayerName2.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double4AwayPlayerName1.Value,awayteam);
  Double4AwayPlayerName1.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double4AwayPlayerName2.Value,awayteam);
  Double4AwayPlayerName2.Value := FloatToStr(pno);
end;
procedure TDoubles.Double5BeforePost(DataSet: TDataSet);
begin
  Double5MatchNo.Value := TMNo;
  Double5DoubleNo.Value := 5;
  Double5EightBall1.Value := RadioButton51.Checked;
  Double5EightBall2.Value := RadioButton52.Checked;
  pno := GetPlayerNo(Double5HomePlayerName1.Value,hometeam);
  Double5HomePlayerName1.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double5HomePlayerName2.Value,hometeam);
  Double5HomePlayerName2.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double5AwayPlayerName1.Value,awayteam);
  Double5AwayPlayerName1.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double5AwayPlayerName2.Value,awayteam);
  Double5AwayPlayerName2.Value := FloatToStr(pno);
end;
procedure TDoubles.Double6BeforePost(DataSet: TDataSet);
begin
  Double6MatchNo.Value := TMNo;
  Double6DoubleNo.Value := 6;
  Double6EightBall1.Value := RadioButton61.Checked;
  Double6EightBall2.Value := RadioButton62.Checked;
  pno := GetPlayerNo(Double6HomePlayerName1.Value,hometeam);
  Double6HomePlayerName1.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double6HomePlayerName2.Value,hometeam);
  Double6HomePlayerName2.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double6AwayPlayerName1.Value,awayteam);
  Double6AwayPlayerName1.Value := FloatToStr(pno);
  pno := GetPlayerNo(Double6AwayPlayerName2.Value,awayteam);
  Double6AwayPlayerName2.Value := FloatToStr(pno);
end;

procedure TDoubles.BitBtn2Click(Sender: TObject);
begin
  Close;
end;

end.
