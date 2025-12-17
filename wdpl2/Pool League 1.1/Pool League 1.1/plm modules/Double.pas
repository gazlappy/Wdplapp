unit Double;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  Db, DBTables, StdCtrls, Buttons, DBCtrls, Mask;

type
  TDoubles = class(TForm)
    SpeedButton1: TSpeedButton;
    SpeedButton2: TSpeedButton;
    ListBox1: TListBox;
    DBEdit1: TDBEdit;
    DBCheckBox1: TDBCheckBox;
    DBCheckBox2: TDBCheckBox;
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
    DBCheckBox4: TDBCheckBox;
    DBCheckBox5: TDBCheckBox;
    DBCheckBox6: TDBCheckBox;
    DBCheckBox7: TDBCheckBox;
    DBCheckBox8: TDBCheckBox;
    DBCheckBox9: TDBCheckBox;
    DBCheckBox10: TDBCheckBox;
    DBCheckBox11: TDBCheckBox;
    DBCheckBox12: TDBCheckBox;
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
    DBCheckBox3: TDBCheckBox;
    Double1MatchNo: TFloatField;
    Double1DoubleNo: TFloatField;
    Double1HomePlayerName1: TStringField;
    Double1HomePlayerName2: TStringField;
    Double1AwayPlayerName1: TStringField;
    Double1AwayPlayerName2: TStringField;
    Double1Winner: TStringField;
    Double1EightBall: TBooleanField;
    Double3MatchNo: TFloatField;
    Double3DoubleNo: TFloatField;
    Double3HomePlayerName1: TStringField;
    Double3HomePlayerName2: TStringField;
    Double3AwayPlayerName1: TStringField;
    Double3AwayPlayerName2: TStringField;
    Double3Winner: TStringField;
    Double3EightBall: TBooleanField;
    Double5MatchNo: TFloatField;
    Double5DoubleNo: TFloatField;
    Double5HomePlayerName1: TStringField;
    Double5HomePlayerName2: TStringField;
    Double5AwayPlayerName1: TStringField;
    Double5AwayPlayerName2: TStringField;
    Double5Winner: TStringField;
    Double5EightBall: TBooleanField;
    Double2MatchNo: TFloatField;
    Double2DoubleNo: TFloatField;
    Double2HomePlayerName1: TStringField;
    Double2HomePlayerName2: TStringField;
    Double2AwayPlayerName1: TStringField;
    Double2AwayPlayerName2: TStringField;
    Double2Winner: TStringField;
    Double2EightBall: TBooleanField;
    Double4MatchNo: TFloatField;
    Double4DoubleNo: TFloatField;
    Double4HomePlayerName1: TStringField;
    Double4HomePlayerName2: TStringField;
    Double4AwayPlayerName1: TStringField;
    Double4AwayPlayerName2: TStringField;
    Double4Winner: TStringField;
    Double4EightBall: TBooleanField;
    Double6MatchNo: TFloatField;
    Double6DoubleNo: TFloatField;
    Double6HomePlayerName1: TStringField;
    Double6HomePlayerName2: TStringField;
    Double6AwayPlayerName1: TStringField;
    Double6AwayPlayerName2: TStringField;
    Double6Winner: TStringField;
    Double6EightBall: TBooleanField;
    LeagueQueryNoDoubles: TIntegerField;
    LeagueQueryMaxDoubles: TIntegerField;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  Doubles: TDoubles;

implementation

{$R *.DFM}
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
  ShowModal;
end;

procedure TDoubles.FormClose(Sender: TObject; var Action: TCloseAction);
begin
  Double1.Close;
  Double2.Close;
  Double3.Close;
  Double4.Close;
  Double5.Close;
  Double6.Close;
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
      RemoveHomePlayer(Double1HomePlayerName1.Value);
      RemoveAwayPlayer(Double1AwayPlayerName1.Value);
      RemoveHomePlayer(Double1HomePlayerName2.Value);
      RemoveAwayPlayer(Double1AwayPlayerName2.Value);
    end
    else
    begin
      Double1.Insert;
      Double1EightBall.Value := False;
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
      RemoveHomePlayer(Double2HomePlayerName1.Value);
      RemoveAwayPlayer(Double2AwayPlayerName1.Value);
      RemoveHomePlayer(Double2HomePlayerName2.Value);
      RemoveAwayPlayer(Double2AwayPlayerName2.Value);
    end
    else
    begin
      Double2.Insert;
      Double2EightBall.Value := False;
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
      RemoveHomePlayer(Double3HomePlayerName1.Value);
      RemoveAwayPlayer(Double3AwayPlayerName1.Value);
      RemoveHomePlayer(Double3HomePlayerName2.Value);
      RemoveAwayPlayer(Double3AwayPlayerName2.Value);
    end
    else
    begin
      Double3.Insert;
      Double3EightBall.Value := False;
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
      RemoveHomePlayer(Double4HomePlayerName1.Value);
      RemoveAwayPlayer(Double4AwayPlayerName1.Value);
      RemoveHomePlayer(Double4HomePlayerName2.Value);
      RemoveAwayPlayer(Double4AwayPlayerName2.Value);
    end
    else
    begin
      Double4.Insert;
      Double4EightBall.Value := False;
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
      RemoveHomePlayer(Double5HomePlayerName1.Value);
      RemoveAwayPlayer(Double5AwayPlayerName1.Value);
      RemoveHomePlayer(Double5HomePlayerName2.Value);
      RemoveAwayPlayer(Double5AwayPlayerName2.Value);
    end
    else
    begin
      Double5.Insert;
      Double5EightBall.Value := False;
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
      RemoveHomePlayer(Double6HomePlayerName1.Value);
      RemoveAwayPlayer(Double6AwayPlayerName1.Value);
      RemoveHomePlayer(Double6HomePlayerName2.Value);
      RemoveAwayPlayer(Double6AwayPlayerName2.Value);
    end
    else
    begin
      Double6.Insert;
      Double6EightBall.Value := False;
      Double6Winner.Value := 'Away';
    end;
  end;
end;

procedure TDoubles.HideDouble1;
begin
  DBEdit1.Visible := False;
  DBEdit2.Visible := False;
  DBCheckBox1.Visible := False;
  DBCheckBox2.Visible := False;
end;

procedure TDoubles.HideDouble2;
begin
  DBEdit3.Visible := False;
  DBEdit4.Visible := False;
  DBCheckBox3.Visible := False;
  DBCheckBox4.Visible := False;
end;
procedure TDoubles.HideDouble3;
begin
  DBEdit5.Visible := False;
  DBEdit6.Visible := False;
  DBCheckBox5.Visible := False;
  DBCheckBox6.Visible := False;
end;
procedure TDoubles.HideDouble4;
begin
  DBEdit7.Visible := False;
  DBEdit8.Visible := False;
  DBCheckBox7.Visible := False;
  DBCheckBox8.Visible := False;
end;
procedure TDoubles.HideDouble5;
begin
  DBEdit9.Visible := False;
  DBEdit10.Visible := False;
  DBCheckBox9.Visible := False;
  DBCheckBox10.Visible := False;
end;
procedure TDoubles.HideDouble6;
begin
  DBEdit11.Visible := False;
  DBEdit12.Visible := False;
  DBCheckBox11.Visible := False;
  DBCheckBox12.Visible := False;
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
    for i := 1 to LeagueQueryMaxSingles.Value do
    begin
    ListBox1.Items.Add(PlayerQueryPlayerName.Value);
    end;
    PlayerQuery.Next;
  end;
end;

procedure TSingles.AssignAwayPlayers;
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
    for i := 1 to LeagueQueryMaxSingles.Value do
    begin
    ListBox2.Items.Add(PlayerQueryPlayerName.Value);
    end;
    PlayerQuery.Next;
  end;
end;

procedure TSingles.RemoveAwayPlayer(APN: String);
var i: integer;
begin
//Take out first instance only of Away Player
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

procedure TSingles.RemoveHomePlayer(HPN: String);
var i: integer;
begin
//Take out first instance only of Home Player
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


end.
