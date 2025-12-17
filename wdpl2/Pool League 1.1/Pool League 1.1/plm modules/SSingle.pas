unit SSingle;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  Db, DBTables, StdCtrls, DBCtrls, Mask, Buttons;

type
  TSSingles = class(TForm)
    ListBox1: TListBox;
    DBEdit1: TDBEdit;
    DBCheckBox1: TDBCheckBox;
    DBCheckBox2: TDBCheckBox;
    DBEdit2: TDBEdit;
    ListBox2: TListBox;
    Single1: TTable;
    SS1: TDataSource;
    Single1MatchNo: TFloatField;
    Single1SingleNo: TFloatField;
    Single1HomePlayerName: TStringField;
    Single1AwayPlayerName: TStringField;
    Single1Winner: TStringField;
    Single1EightBall: TBooleanField;
    PlayerQuery: TQuery;
    PlayerQueryPlayerName: TStringField;
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
    DBCheckBox7: TDBCheckBox;
    DBCheckBox8: TDBCheckBox;
    DBCheckBox9: TDBCheckBox;
    DBCheckBox10: TDBCheckBox;
    DBCheckBox11: TDBCheckBox;
    DBCheckBox12: TDBCheckBox;
    DBCheckBox13: TDBCheckBox;
    DBCheckBox14: TDBCheckBox;
    DBCheckBox15: TDBCheckBox;
    DBCheckBox16: TDBCheckBox;
    DBCheckBox17: TDBCheckBox;
    DBCheckBox18: TDBCheckBox;
    DBCheckBox19: TDBCheckBox;
    DBCheckBox20: TDBCheckBox;
    DBCheckBox21: TDBCheckBox;
    DBCheckBox22: TDBCheckBox;
    DBCheckBox23: TDBCheckBox;
    DBCheckBox24: TDBCheckBox;
    Single2: TTable;
    SS2: TDataSource;
    Single3: TTable;
    SS3: TDataSource;
    SS12: TDataSource;
    Single11: TTable;
    Single5: TTable;
    SS8: TDataSource;
    Single7: TTable;
    SS10: TDataSource;
    Single9: TTable;
    SS4: TDataSource;
    Single4: TTable;
    SS6: TDataSource;
    Single6: TTable;
    Single10: TTable;
    SS5: TDataSource;
    Single8: TTable;
    Single12: TTable;
    SS11: TDataSource;
    SS9: TDataSource;
    SS7: TDataSource;
    BitBtn1: TBitBtn;
    BitBtn2: TBitBtn;
    LeagueQuery: TQuery;
    LeagueQueryNoSingles: TIntegerField;
    Single3MatchNo: TFloatField;
    Single3SingleNo: TFloatField;
    Single3HomePlayerName: TStringField;
    Single3AwayPlayerName: TStringField;
    Single3Winner: TStringField;
    Single3EightBall: TBooleanField;
    Single5MatchNo: TFloatField;
    Single5SingleNo: TFloatField;
    Single5HomePlayerName: TStringField;
    Single5AwayPlayerName: TStringField;
    Single5Winner: TStringField;
    Single5EightBall: TBooleanField;
    Single7MatchNo: TFloatField;
    Single7SingleNo: TFloatField;
    Single7HomePlayerName: TStringField;
    Single7AwayPlayerName: TStringField;
    Single7Winner: TStringField;
    Single7EightBall: TBooleanField;
    Single9MatchNo: TFloatField;
    Single9SingleNo: TFloatField;
    Single9HomePlayerName: TStringField;
    Single9AwayPlayerName: TStringField;
    Single9Winner: TStringField;
    Single9EightBall: TBooleanField;
    Single11MatchNo: TFloatField;
    Single11SingleNo: TFloatField;
    Single11HomePlayerName: TStringField;
    Single11AwayPlayerName: TStringField;
    Single11Winner: TStringField;
    Single11EightBall: TBooleanField;
    Single2MatchNo: TFloatField;
    Single2SingleNo: TFloatField;
    Single2HomePlayerName: TStringField;
    Single2AwayPlayerName: TStringField;
    Single2Winner: TStringField;
    Single2EightBall: TBooleanField;
    Single4MatchNo: TFloatField;
    Single4SingleNo: TFloatField;
    Single4HomePlayerName: TStringField;
    Single4AwayPlayerName: TStringField;
    Single4Winner: TStringField;
    Single4EightBall: TBooleanField;
    Single6MatchNo: TFloatField;
    Single6SingleNo: TFloatField;
    Single6HomePlayerName: TStringField;
    Single6AwayPlayerName: TStringField;
    Single6Winner: TStringField;
    Single6EightBall: TBooleanField;
    Single8MatchNo: TFloatField;
    Single8SingleNo: TFloatField;
    Single8HomePlayerName: TStringField;
    Single8AwayPlayerName: TStringField;
    Single8Winner: TStringField;
    Single8EightBall: TBooleanField;
    Single10MatchNo: TFloatField;
    Single10SingleNo: TFloatField;
    Single10HomePlayerName: TStringField;
    Single10AwayPlayerName: TStringField;
    Single10Winner: TStringField;
    Single10EightBall: TBooleanField;
    Single12MatchNo: TFloatField;
    Single12SingleNo: TFloatField;
    Single12HomePlayerName: TStringField;
    Single12AwayPlayerName: TStringField;
    Single12Winner: TStringField;
    Single12EightBall: TBooleanField;
    LeagueQueryMaxSingles: TIntegerField;
    SpeedButton1: TSpeedButton;
    SpeedButton2: TSpeedButton;
    PlayerQueryPlayerNo: TFloatField;
    PQuery: TQuery;
    PQueryPlayerNo: TFloatField;
    Player: TTable;
    PlayerSource: TDataSource;
    PlayerPlayerNo: TFloatField;
    PlayerPlayerName: TStringField;
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
    procedure Single1BeforePost(DataSet: TDataSet);
    procedure Single2BeforePost(DataSet: TDataSet);
    procedure Single3BeforePost(DataSet: TDataSet);
    procedure Single4BeforePost(DataSet: TDataSet);
    procedure Single5BeforePost(DataSet: TDataSet);
    procedure Single6BeforePost(DataSet: TDataSet);
    procedure Single7BeforePost(DataSet: TDataSet);
    procedure Single8BeforePost(DataSet: TDataSet);
    procedure Single9BeforePost(DataSet: TDataSet);
    procedure Single10BeforePost(DataSet: TDataSet);
    procedure Single11BeforePost(DataSet: TDataSet);
    procedure Single12BeforePost(DataSet: TDataSet);
    procedure FormShow;
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
    procedure BitBtn1Click(Sender: TObject);
    procedure ListBox1DblClick(Sender: TObject);
    procedure SpeedButton1Click(Sender: TObject);
    procedure SpeedButton2Click(Sender: TObject);
    procedure ListBox2DblClick(Sender: TObject);
    procedure ListBox2Exit(Sender: TObject);
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
    procedure GetSingle1;
    procedure GetSingle2;
    procedure GetSingle3;
    procedure GetSingle4;
    procedure GetSingle5;
    procedure GetSingle6;
    procedure GetSingle7;
    procedure GetSingle8;
    procedure GetSingle9;
    procedure GetSingle10;
    procedure GetSingle11;
    procedure GetSingle12;
    procedure HideSingle1;
    procedure HideSingle2;
    procedure HideSingle3;
    procedure HideSingle4;
    procedure HideSingle5;
    procedure HideSingle6;
    procedure HideSingle7;
    procedure HideSingle8;
    procedure HideSingle9;
    procedure HideSingle10;
    procedure HideSingle11;
    procedure HideSingle12;
    function GetPlayerNo(RequiredName, RequiredTeam: String): Double;
    function GetPlayerName(RequiredNo: Double): String;
    function AddToList1(RequiredName: String): Double;
    function RemoveFromList1(RequiredName: String): Double;
    function AddToList2(RequiredName: String): Double;
    function RemoveFromList2(RequiredName: String): Double;
{ Public declarations }
  end;

var
  SSingles: TSSingles;

implementation

uses Main, NMatch, Player;

{$R *.DFM}

procedure TSSingles.Edit(MatchNo: Double; ATN, HTN: String);
begin
  LeagueQuery.Close;
  LeagueQuery.Open;
  LeagueQuery.First;
  if LeagueQueryMaxSingles.Value = 0 then
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
  Singles.Caption := 'Match No. ' + FloatToStr(TMNo) + ' Singles';
  FormShow;
end;

procedure TSSingles.FormClose(Sender: TObject; var Action: TCloseAction);
begin
  Single1.Close;
  Single2.Close;
  Single3.Close;
  Single4.Close;
  Single5.Close;
  Single6.Close;
  Single7.Close;
  Single8.Close;
  Single9.Close;
  Single10.Close;
  Single11.Close;
  Single12.Close;
  Action := caFree;
  NMForm.SpeedButton1.Enabled := True;
end;

function TSSingles.AddToList1(RequiredName: String): Double;
begin
  if RequiredName <> '** Conceeded **' then
    if RequiredName <> 'Unknown' then
      if RequiredName <> '' then
        ListBox1.Items.Add(RequiredName);
end;

function TSSingles.RemoveFromList1(RequiredName: String): Double;
begin
  if RequiredName <> '** Conceeded **' then
    ListBox1.Items.Delete(ListBox1.ItemIndex);
end;

function TSSingles.AddToList2(RequiredName: String): Double;
begin
  if RequiredName <> '** Conceeded **' then
    if RequiredName <> 'Unknown' then
      if RequiredName <> '' then
        ListBox2.Items.Add(RequiredName);
end;

function TSSingles.RemoveFromList2(RequiredName: String): Double;
begin
  if RequiredName <> '** Conceeded **' then
    ListBox2.Items.Delete(ListBox2.ItemIndex);
end;

function TSSingles.GetPlayerNo(RequiredName, RequiredTeam: String): Double;
begin
  if RequiredName = '** Conceeded **' then
  begin
    Result := 999999;
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

function TSSingles.GetPlayerName(RequiredNo: Double): String;
begin
  if RequiredNo = 999999 then
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

// Before Postings Start Here
procedure TSSingles.Single1BeforePost(DataSet: TDataSet);
begin
  Single1MatchNo.Value := TMNo;
  Single1SingleNo.Value := 1;
  pno := GetPlayerNo(Single1HomePlayerName.Value,hometeam);
  Single1HomePlayerName.Value := FloatToStr(pno);
  pno := GetPlayerNo(Single1AwayPlayerName.Value,awayteam);
  Single1AwayPlayerName.Value := FloatToStr(pno);
end;
procedure TSSingles.Single2BeforePost(DataSet: TDataSet);
begin
  Single2MatchNo.Value := TMNo;
  Single2SingleNo.Value := 2;
  pno := GetPlayerNo(Single2HomePlayerName.Value,hometeam);
  Single2HomePlayerName.Value := FloatToStr(pno);
  pno := GetPlayerNo(Single2AwayPlayerName.Value,awayteam);
  Single2AwayPlayerName.Value := FloatToStr(pno);
end;
procedure TSSingles.Single3BeforePost(DataSet: TDataSet);
begin
  Single3MatchNo.Value := TMNo;
  Single3SingleNo.Value := 3;
  pno := GetPlayerNo(Single3HomePlayerName.Value,hometeam);
  Single3HomePlayerName.Value := FloatToStr(pno);
  pno := GetPlayerNo(Single3AwayPlayerName.Value,awayteam);
  Single3AwayPlayerName.Value := FloatToStr(pno);
end;
procedure TSSingles.Single4BeforePost(DataSet: TDataSet);
begin
  Single4MatchNo.Value := TMNo;
  Single4SingleNo.Value := 4;
  pno := GetPlayerNo(Single4HomePlayerName.Value,hometeam);
  Single4HomePlayerName.Value := FloatToStr(pno);
  pno := GetPlayerNo(Single4AwayPlayerName.Value,awayteam);
  Single4AwayPlayerName.Value := FloatToStr(pno);
end;
procedure TSSingles.Single5BeforePost(DataSet: TDataSet);
begin
  Single5MatchNo.Value := TMNo;
  Single5SingleNo.Value := 5;
  pno := GetPlayerNo(Single5HomePlayerName.Value,hometeam);
  Single5HomePlayerName.Value := FloatToStr(pno);
  pno := GetPlayerNo(Single5AwayPlayerName.Value,awayteam);
  Single5AwayPlayerName.Value := FloatToStr(pno);
end;
procedure TSSingles.Single6BeforePost(DataSet: TDataSet);
begin
  Single6MatchNo.Value := TMNo;
  Single6SingleNo.Value := 6;
  pno := GetPlayerNo(Single6HomePlayerName.Value,hometeam);
  Single6HomePlayerName.Value := FloatToStr(pno);
  pno := GetPlayerNo(Single6AwayPlayerName.Value,awayteam);
  Single6AwayPlayerName.Value := FloatToStr(pno);
end;
procedure TSSingles.Single7BeforePost(DataSet: TDataSet);
begin
  Single7MatchNo.Value := TMNo;
  Single7SingleNo.Value := 7;
  pno := GetPlayerNo(Single7HomePlayerName.Value,hometeam);
  Single7HomePlayerName.Value := FloatToStr(pno);
  pno := GetPlayerNo(Single7AwayPlayerName.Value,awayteam);
  Single7AwayPlayerName.Value := FloatToStr(pno);
end;
procedure TSSingles.Single8BeforePost(DataSet: TDataSet);
begin
  Single8MatchNo.Value := TMNo;
  Single8SingleNo.Value := 8;
  pno := GetPlayerNo(Single8HomePlayerName.Value,hometeam);
  Single8HomePlayerName.Value := FloatToStr(pno);
  pno := GetPlayerNo(Single8AwayPlayerName.Value,awayteam);
  Single8AwayPlayerName.Value := FloatToStr(pno);
end;
procedure TSSingles.Single9BeforePost(DataSet: TDataSet);
begin
  Single9MatchNo.Value := TMNo;
  Single9SingleNo.Value := 9;
  pno := GetPlayerNo(Single9HomePlayerName.Value,hometeam);
  Single9HomePlayerName.Value := FloatToStr(pno);
  pno := GetPlayerNo(Single9AwayPlayerName.Value,awayteam);
  Single9AwayPlayerName.Value := FloatToStr(pno);
end;
procedure TSSingles.Single10BeforePost(DataSet: TDataSet);
begin
  Single10MatchNo.Value := TMNo;
  Single10SingleNo.Value := 10;
  pno := GetPlayerNo(Single10HomePlayerName.Value,hometeam);
  Single10HomePlayerName.Value := FloatToStr(pno);
  pno := GetPlayerNo(Single10AwayPlayerName.Value,awayteam);
  Single10AwayPlayerName.Value := FloatToStr(pno);
end;
procedure TSSingles.Single11BeforePost(DataSet: TDataSet);
begin
  Single11MatchNo.Value := TMNo;
  Single11SingleNo.Value := 11;
  pno := GetPlayerNo(Single11HomePlayerName.Value,hometeam);
  Single11HomePlayerName.Value := FloatToStr(pno);
  pno := GetPlayerNo(Single11AwayPlayerName.Value,awayteam);
  Single11AwayPlayerName.Value := FloatToStr(pno);
end;
procedure TSSingles.Single12BeforePost(DataSet: TDataSet);
begin
  Single12MatchNo.Value := TMNo;
  Single12SingleNo.Value := 12;
  pno := GetPlayerNo(Single12HomePlayerName.Value,hometeam);
  Single12HomePlayerName.Value := FloatToStr(pno);
  pno := GetPlayerNo(Single12AwayPlayerName.Value,awayteam);
  Single12AwayPlayerName.Value := FloatToStr(pno);
end;

procedure TSSingles.FormShow;
begin
  if LeagueQueryNoSingles.Value > 0 then
    GetSingle1
  else
    HideSingle1;
  if LeagueQueryNoSingles.Value > 1 then
    GetSingle2
  else
    HideSingle2;
  if LeagueQueryNoSingles.Value > 2 then
    GetSingle3
  else
    HideSingle3;
  if LeagueQueryNoSingles.Value > 3 then
    GetSingle4
  else
    HideSingle4;
  if LeagueQueryNoSingles.Value > 4 then
    GetSingle5
  else
    HideSingle5;
  if LeagueQueryNoSingles.Value > 5 then
    GetSingle6
  else
    HideSingle6;
  if LeagueQueryNoSingles.Value > 6 then
    GetSingle7
  else
    HideSingle7;
  if LeagueQueryNoSingles.Value > 7 then
    GetSingle8
  else
    HideSingle8;
  if LeagueQueryNoSingles.Value > 8 then
    GetSingle9
  else
    HideSingle9;
  if LeagueQueryNoSingles.Value > 9 then
    GetSingle10
  else
    HideSingle10;
  if LeagueQueryNoSingles.Value > 10 then
    GetSingle11
  else
    HideSingle11;
  if LeagueQueryNoSingles.Value > 11 then
    GetSingle12
  else
    HideSingle12;
end;

// 12 x Get Single start here
procedure TSSingles.GetSingle1;
begin
  with Single1 do
  begin
    Open;
    EditKey;
    Single1MatchNo.Value := TMNo;
    Single1SingleNo.Value := 1;
    if GotoKey then
    begin
      Single1.Edit;
      pno := StrToFloat(Single1HomePlayerName.Value);
      Single1HomePlayerName.Value := GetPlayerName(pno);
      RemoveHomePlayer(Single1HomePlayerName.Value);
      pno := StrToFloat(Single1AwayPlayerName.Value);
      Single1AwayPlayerName.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Single1AwayPlayerName.Value);
    end
    else
    begin
      Single1.Insert;
      Single1EightBall.Value := False;
      Single1Winner.Value := 'Away';
    end;
  end;
end;

procedure TSSingles.GetSingle2;
begin
  with Single2 do
  begin
    Open;
    EditKey;
    Single2MatchNo.Value := TMNo;
    Single2SingleNo.Value := 2;
    if GotoKey then
    begin
      Single2.Edit;
      pno := StrToFloat(Single2HomePlayerName.Value);
      Single2HomePlayerName.Value := GetPlayerName(pno);
      RemoveHomePlayer(Single2HomePlayerName.Value);
      pno := StrToFloat(Single2AwayPlayerName.Value);
      Single2AwayPlayerName.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Single2AwayPlayerName.Value);
    end
    else
    begin
      Single2.Insert;
      Single2EightBall.Value := False;
      Single2Winner.Value := 'Away';
    end;
  end;
end;

procedure TSSingles.GetSingle3;
begin
  with Single3 do
  begin
    Open;
    EditKey;
    Single3MatchNo.Value := TMNo;
    Single3SingleNo.Value := 3;
    if GotoKey then
    begin
      Single3.Edit;
      pno := StrToFloat(Single3HomePlayerName.Value);
      Single3HomePlayerName.Value := GetPlayerName(pno);
      RemoveHomePlayer(Single3HomePlayerName.Value);
      pno := StrToFloat(Single3AwayPlayerName.Value);
      Single3AwayPlayerName.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Single3AwayPlayerName.Value);
    end
    else
    begin
      Single3.Insert;
      Single3EightBall.Value := False;
      Single3Winner.Value := 'Away';
    end;
  end;
end;

procedure TSSingles.GetSingle4;
begin
  with Single4 do
  begin
    Open;
    EditKey;
    Single4MatchNo.Value := TMNo;
    Single4SingleNo.Value := 4;
    if GotoKey then
    begin
      Single4.Edit;
      pno := StrToFloat(Single4HomePlayerName.Value);
      Single4HomePlayerName.Value := GetPlayerName(pno);
      RemoveHomePlayer(Single4HomePlayerName.Value);
      pno := StrToFloat(Single4AwayPlayerName.Value);
      Single4AwayPlayerName.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Single4AwayPlayerName.Value);
    end
    else
    begin
      Single4.Insert;
      Single4EightBall.Value := False;
      Single4Winner.Value := 'Away';
    end;
  end;
end;

procedure TSSingles.GetSingle5;
begin
  with Single5 do
  begin
    Open;
    EditKey;
    Single5MatchNo.Value := TMNo;
    Single5SingleNo.Value := 5;
    if GotoKey then
    begin
      Single5.Edit;
      pno := StrToFloat(Single5HomePlayerName.Value);
      Single5HomePlayerName.Value := GetPlayerName(pno);
      RemoveHomePlayer(Single5HomePlayerName.Value);
      pno := StrToFloat(Single5AwayPlayerName.Value);
      Single5AwayPlayerName.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Single5AwayPlayerName.Value);
    end
    else
    begin
      Single5.Insert;
      Single5EightBall.Value := False;
      Single5Winner.Value := 'Away';
    end;
  end;
end;

procedure TSSingles.GetSingle6;
begin
  with Single6 do
  begin
    Open;
    EditKey;
    Single6MatchNo.Value := TMNo;
    Single6SingleNo.Value := 6;
    if GotoKey then
    begin
      Single6.Edit;
      pno := StrToFloat(Single6HomePlayerName.Value);
      Single6HomePlayerName.Value := GetPlayerName(pno);
      RemoveHomePlayer(Single6HomePlayerName.Value);
      pno := StrToFloat(Single6AwayPlayerName.Value);
      Single6AwayPlayerName.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Single6AwayPlayerName.Value);
    end
    else
    begin
      Single6.Insert;
      Single6EightBall.Value := False;
      Single6Winner.Value := 'Away';
    end;
  end;
end;

procedure TSSingles.GetSingle7;
begin
  with Single7 do
  begin
    Open;
    EditKey;
    Single7MatchNo.Value := TMNo;
    Single7SingleNo.Value := 7;
    if GotoKey then
    begin
      Single7.Edit;
      pno := StrToFloat(Single7HomePlayerName.Value);
      Single7HomePlayerName.Value := GetPlayerName(pno);
      RemoveHomePlayer(Single7HomePlayerName.Value);
      pno := StrToFloat(Single7AwayPlayerName.Value);
      Single7AwayPlayerName.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Single7AwayPlayerName.Value);
    end
    else
    begin
      Single7.Insert;
      Single7EightBall.Value := False;
      Single7Winner.Value := 'Away';
    end;
  end;
end;

procedure TSSingles.GetSingle8;
begin
  with Single8 do
  begin
    Open;
    EditKey;
    Single8MatchNo.Value := TMNo;
    Single8SingleNo.Value := 8;
    if GotoKey then
    begin
      Single8.Edit;
      pno := StrToFloat(Single8HomePlayerName.Value);
      Single8HomePlayerName.Value := GetPlayerName(pno);
      RemoveHomePlayer(Single8HomePlayerName.Value);
      pno := StrToFloat(Single8AwayPlayerName.Value);
      Single8AwayPlayerName.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Single8AwayPlayerName.Value);
    end
    else
    begin
      Single8.Insert;
      Single8EightBall.Value := False;
      Single8Winner.Value := 'Away';
    end;
  end;
end;

procedure TSSingles.GetSingle9;
begin
  with Single9 do
  begin
    Open;
    EditKey;
    Single9MatchNo.Value := TMNo;
    Single9SingleNo.Value := 9;
    if GotoKey then
    begin
      Single9.Edit;
      pno := StrToFloat(Single9HomePlayerName.Value);
      Single9HomePlayerName.Value := GetPlayerName(pno);
      RemoveHomePlayer(Single9HomePlayerName.Value);
      pno := StrToFloat(Single9AwayPlayerName.Value);
      Single9AwayPlayerName.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Single9AwayPlayerName.Value);
    end
    else
    begin
      Single9.Insert;
      Single9EightBall.Value := False;
      Single9Winner.Value := 'Away';
    end;
  end;
end;

procedure TSSingles.GetSingle10;
begin
  with Single10 do
  begin
    Open;
    EditKey;
    Single10MatchNo.Value := TMNo;
    Single10SingleNo.Value := 10;
    if GotoKey then
    begin
      Single10.Edit;
      pno := StrToFloat(Single10HomePlayerName.Value);
      Single10HomePlayerName.Value := GetPlayerName(pno);
      RemoveHomePlayer(Single10HomePlayerName.Value);
      pno := StrToFloat(Single10AwayPlayerName.Value);
      Single10AwayPlayerName.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Single10AwayPlayerName.Value);
    end
    else
    begin
      Single10.Insert;
      Single10EightBall.Value := False;
      Single10Winner.Value := 'Away';
    end;
  end;
end;

procedure TSSingles.GetSingle11;
begin
  with Single11 do
  begin
    Open;
    EditKey;
    Single11MatchNo.Value := TMNo;
    Single11SingleNo.Value := 11;
    if GotoKey then
    begin
      Single11.Edit;
      pno := StrToFloat(Single11HomePlayerName.Value);
      Single11HomePlayerName.Value := GetPlayerName(pno);
      RemoveHomePlayer(Single11HomePlayerName.Value);
      pno := StrToFloat(Single11AwayPlayerName.Value);
      Single11AwayPlayerName.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Single11AwayPlayerName.Value);
    end
    else
    begin
      Single11.Insert;
      Single11EightBall.Value := False;
      Single11Winner.Value := 'Away';
    end;
  end;
end;

procedure TSSingles.GetSingle12;
begin
  with Single12 do
  begin
    Open;
    EditKey;
    Single12MatchNo.Value := TMNo;
    Single12SingleNo.Value := 12;
    if GotoKey then
    begin
      Single12.Edit;
      pno := StrToFloat(Single12HomePlayerName.Value);
      Single12HomePlayerName.Value := GetPlayerName(pno);
      RemoveHomePlayer(Single12HomePlayerName.Value);
      pno := StrToFloat(Single12AwayPlayerName.Value);
      Single12AwayPlayerName.Value := GetPlayerName(pno);
      RemoveAwayPlayer(Single12AwayPlayerName.Value);
    end
    else
    begin
      Single12.Insert;
      Single12EightBall.Value := False;
      Single12Winner.Value := 'Away';
    end;
  end;
end;

procedure TSSingles.HideSingle1;
begin
  DBEdit1.Visible := False;
  DBEdit2.Visible := False;
  DBCheckBox1.Visible := False;
  DBCheckBox2.Visible := False;
end;

procedure TSSingles.HideSingle2;
begin
  DBEdit3.Visible := False;
  DBEdit4.Visible := False;
  DBCheckBox3.Visible := False;
  DBCheckBox4.Visible := False;
end;
procedure TSSingles.HideSingle3;
begin
  DBEdit5.Visible := False;
  DBEdit6.Visible := False;
  DBCheckBox5.Visible := False;
  DBCheckBox6.Visible := False;
end;
procedure TSSingles.HideSingle4;
begin
  DBEdit7.Visible := False;
  DBEdit8.Visible := False;
  DBCheckBox7.Visible := False;
  DBCheckBox8.Visible := False;
end;
procedure TSSingles.HideSingle5;
begin
  DBEdit9.Visible := False;
  DBEdit10.Visible := False;
  DBCheckBox9.Visible := False;
  DBCheckBox10.Visible := False;
end;
procedure TSSingles.HideSingle6;
begin
  DBEdit11.Visible := False;
  DBEdit12.Visible := False;
  DBCheckBox11.Visible := False;
  DBCheckBox12.Visible := False;
end;
procedure TSSingles.HideSingle7;
begin
  DBEdit13.Visible := False;
  DBEdit14.Visible := False;
  DBCheckBox13.Visible := False;
  DBCheckBox14.Visible := False;
end;
procedure TSSingles.HideSingle8;
begin
  DBEdit15.Visible := False;
  DBEdit16.Visible := False;
  DBCheckBox15.Visible := False;
  DBCheckBox16.Visible := False;
end;
procedure TSSingles.HideSingle9;
begin
  DBEdit17.Visible := False;
  DBEdit18.Visible := False;
  DBCheckBox17.Visible := False;
  DBCheckBox18.Visible := False;
end;
procedure TSSingles.HideSingle10;
begin
  DBEdit19.Visible := False;
  DBEdit20.Visible := False;
  DBCheckBox19.Visible := False;
  DBCheckBox20.Visible := False;
end;
procedure TSSingles.HideSingle11;
begin
  DBEdit21.Visible := False;
  DBEdit22.Visible := False;
  DBCheckBox21.Visible := False;
  DBCheckBox22.Visible := False;
end;
procedure TSSingles.HideSingle12;
begin
  DBEdit23.Visible := False;
  DBEdit24.Visible := False;
  DBCheckBox23.Visible := False;
  DBCheckBox24.Visible := False;
end;

procedure TSSingles.AssignHomePlayers;
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
  ListBox1.Items.Add('** Conceeded **');
end;

procedure TSSingles.AssignAwayPlayers;
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
  ListBox2.Items.Add('** Conceeded **');
end;

procedure TSSingles.RemoveAwayPlayer(APN: String);
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

procedure TSSingles.RemoveHomePlayer(HPN: String);
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

// Home Player Drag and Drop Starts Here
// Player 1

procedure TSSingles.DBEdit1DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit1DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit1.Text);
  DBEdit1.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Single1HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit1.Text);
end;

procedure TSSingles.DBEdit1DblClick(Sender: TObject);
begin
  AddToList1(DBEdit1.Text);
  DBEdit1.Text := '';
  Single1HomePlayerName.Value := '';
end;
// Player 2
procedure TSSingles.DBEdit3DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit3DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit3.Text);
  DBEdit3.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Single2HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit3.Text);
end;

procedure TSSingles.DBEdit3DblClick(Sender: TObject);
begin
  AddToList1(DBEdit3.Text);
  DBEdit3.Text := '';
  Single2HomePlayerName.Value := '';
end;
// Player 3
procedure TSSingles.DBEdit5DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit5DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit5.Text);
  DBEdit5.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Single3HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit5.Text);
end;

procedure TSSingles.DBEdit5DblClick(Sender: TObject);
begin
  AddToList1(DBEdit5.Text);
  DBEdit5.Text := '';
  Single3HomePlayerName.Value := '';
end;
// Player 4
procedure TSSingles.DBEdit7DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit7DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit7.Text);
  DBEdit7.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Single4HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit7.Text);
end;

procedure TSSingles.DBEdit7DblClick(Sender: TObject);
begin
  AddToList1(DBEdit7.Text);
  DBEdit7.Text := '';
  Single4HomePlayerName.Value := '';
end;
// Player 5
procedure TSSingles.DBEdit9DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit9DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit9.Text);
  DBEdit9.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Single5HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit9.Text);
end;

procedure TSSingles.DBEdit9DblClick(Sender: TObject);
begin
  AddToList1(DBEdit9.Text);
  DBEdit9.Text := '';
  Single5HomePlayerName.Value := '';
end;
// Player 6
procedure TSSingles.DBEdit11DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit11DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit11.Text);
  DBEdit11.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Single6HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit11.Text);
end;

procedure TSSingles.DBEdit11DblClick(Sender: TObject);
begin
  AddToList1(DBEdit11.Text);
  DBEdit11.Text := '';
  Single6HomePlayerName.Value := '';
end;
// Player 7
procedure TSSingles.DBEdit13DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit13DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit13.Text);
  DBEdit13.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Single7HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit13.Text);
end;

procedure TSSingles.DBEdit13DblClick(Sender: TObject);
begin
  AddToList1(DBEdit13.Text);
  DBEdit13.Text := '';
  Single7HomePlayerName.Value := '';
end;
// Player 8
procedure TSSingles.DBEdit15DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit15DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit15.Text);
  DBEdit15.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Single8HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit15.Text);
end;

procedure TSSingles.DBEdit15DblClick(Sender: TObject);
begin
  AddToList1(DBEdit15.Text);
  DBEdit15.Text := '';
  Single8HomePlayerName.Value := '';
end;
// Player 9
procedure TSSingles.DBEdit17DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit17DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit17.Text);
  DBEdit17.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Single9HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit17.Text);
end;

procedure TSSingles.DBEdit17DblClick(Sender: TObject);
begin
  AddToList1(DBEdit17.Text);
  DBEdit17.Text := '';
  Single9HomePlayerName.Value := '';
end;
// Player 10
procedure TSSingles.DBEdit19DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit19DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit19.Text);
  DBEdit19.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Single10HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit19.Text);
end;

procedure TSSingles.DBEdit19DblClick(Sender: TObject);
begin
  AddToList1(DBEdit19.Text);
  DBEdit19.Text := '';
  Single10HomePlayerName.Value := '';
end;
// Player 11
procedure TSSingles.DBEdit21DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit21DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit21.Text);
  DBEdit21.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Single11HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit21.Text);
end;

procedure TSSingles.DBEdit21DblClick(Sender: TObject);
begin
  AddToList1(DBEdit21.Text);
  DBEdit21.Text := '';
  Single11HomePlayerName.Value := '';
end;
// Player 12
procedure TSSingles.DBEdit23DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox1.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit23DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList1(DBEdit23.Text);
  DBEdit23.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
  Single12HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
  RemoveFromList1(DBEdit23.Text);
end;

procedure TSSingles.DBEdit23DblClick(Sender: TObject);
begin
  AddToList1(DBEdit23.Text);
  DBEdit23.Text := '';
  Single12HomePlayerName.Value := '';
end;
// End of Home Player Drag and Drop

// List Box 1 Procedure Start Here
procedure TSSingles.ListBox1Exit(Sender: TObject);
begin
ListBox1.ItemIndex := -1;
end;

procedure TSSingles.ListBox1DblClick(Sender: TObject);
begin
  if LeagueQueryNoSingles.Value < 1 then abort;
  if DBEdit1.Text = '' then
  begin
    DBEdit1.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
    Single1HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
    RemoveFromList1(DBEdit1.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 2 then abort;
  if DBEdit3.Text = '' then
  begin
    DBEdit3.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
    Single2HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
    RemoveFromList1(DBEdit3.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 3 then abort;
  if DBEdit5.Text = '' then
  begin
    DBEdit5.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
    Single3HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
    RemoveFromList1(DBEdit5.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 4 then abort;
  if DBEdit7.Text = '' then
  begin
    DBEdit7.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
    Single4HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
    RemoveFromList1(DBEdit7.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 5 then abort;
  if DBEdit9.Text = '' then
  begin
    DBEdit9.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
    Single5HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
    RemoveFromList1(DBEdit9.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 6 then abort;
  if DBEdit11.Text = '' then
  begin
    DBEdit11.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
    Single6HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
    RemoveFromList1(DBEdit11.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 7 then abort;
  if DBEdit13.Text = '' then
  begin
    DBEdit13.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
    Single7HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
    RemoveFromList1(DBEdit13.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 8 then abort;
  if DBEdit15.Text = '' then
  begin
    DBEdit15.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
    Single8HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
    RemoveFromList1(DBEdit15.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 9 then abort;
  if DBEdit17.Text = '' then
  begin
    DBEdit17.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
    Single9HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
    RemoveFromList1(DBEdit17.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 10 then abort;
  if DBEdit19.Text = '' then
  begin
    DBEdit19.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
    Single10HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
    RemoveFromList1(DBEdit19.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 11 then abort;
  if DBEdit21.Text = '' then
  begin
    DBEdit21.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
    Single11HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
    RemoveFromList1(DBEdit21.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 12 then abort;
  if DBEdit23.Text = '' then
  begin
    DBEdit23.Text := ListBox1.Items.Strings[ListBox1.ItemIndex];
    Single12HomePlayerName.Value := ListBox1.Items.Strings[ListBox1.ItemIndex];
    RemoveFromList1(DBEdit23.Text);
    Abort;
  end;
end;

// Away Player Drag and Drop Starts Here
// Player 1

procedure TSSingles.DBEdit2DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit2DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit2.Text);
  DBEdit2.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Single1AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit2.Text);
end;

procedure TSSingles.DBEdit2DblClick(Sender: TObject);
begin
  AddToList2(DBEdit2.Text);
  DBEdit2.Text := '';
  Single1AwayPlayerName.Value := '';
end;
// Player 2
procedure TSSingles.DBEdit4DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit4DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit4.Text);
  DBEdit4.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Single2AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit4.Text);
end;

procedure TSSingles.DBEdit4DblClick(Sender: TObject);
begin
  AddToList2(DBEdit4.Text);
  DBEdit4.Text := '';
  Single2AwayPlayerName.Value := '';
end;
// Player 3
procedure TSSingles.DBEdit6DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit6DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit6.Text);
  DBEdit6.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Single3AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit6.Text);
end;

procedure TSSingles.DBEdit6DblClick(Sender: TObject);
begin
  AddToList2(DBEdit6.Text);
  DBEdit6.Text := '';
  Single3AwayPlayerName.Value := '';
end;
// Player 4
procedure TSSingles.DBEdit8DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit8DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit8.Text);
  DBEdit8.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Single4AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit8.Text);
end;

procedure TSSingles.DBEdit8DblClick(Sender: TObject);
begin
  AddToList2(DBEdit8.Text);
  DBEdit8.Text := '';
  Single4AwayPlayerName.Value := '';
end;
// Player 5
procedure TSSingles.DBEdit10DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit10DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit10.Text);
  DBEdit10.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Single5AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit10.Text);
end;

procedure TSSingles.DBEdit10DblClick(Sender: TObject);
begin
  AddToList2(DBEdit10.Text);
  DBEdit10.Text := '';
  Single5AwayPlayerName.Value := '';
end;
// Player 6
procedure TSSingles.DBEdit12DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit12DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit12.Text);
  DBEdit12.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Single6AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit12.Text);
end;

procedure TSSingles.DBEdit12DblClick(Sender: TObject);
begin
  AddToList2(DBEdit12.Text);
  DBEdit12.Text := '';
  Single6AwayPlayerName.Value := '';
end;
// Player 7
procedure TSSingles.DBEdit14DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit14DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit14.Text);
  DBEdit14.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Single7AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit14.Text);
end;

procedure TSSingles.DBEdit14DblClick(Sender: TObject);
begin
  AddToList2(DBEdit14.Text);
  DBEdit14.Text := '';
  Single7AwayPlayerName.Value := '';
end;
// Player 8
procedure TSSingles.DBEdit16DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit16DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit16.Text);
  DBEdit16.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Single8AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit16.Text);
end;

procedure TSSingles.DBEdit16DblClick(Sender: TObject);
begin
  AddToList2(DBEdit16.Text);
  DBEdit16.Text := '';
  Single8AwayPlayerName.Value := '';
end;
// Player 9
procedure TSSingles.DBEdit18DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit18DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit18.Text);
  DBEdit18.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Single9AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit18.Text);
end;

procedure TSSingles.DBEdit18DblClick(Sender: TObject);
begin
  AddToList2(DBEdit18.Text);
  DBEdit18.Text := '';
  Single9AwayPlayerName.Value := '';
end;
// Player 10
procedure TSSingles.DBEdit20DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit20DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit20.Text);
  DBEdit20.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Single10AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit20.Text);
end;

procedure TSSingles.DBEdit20DblClick(Sender: TObject);
begin
  AddToList2(DBEdit20.Text);
  DBEdit20.Text := '';
  if DBEdit20.Text <> '** Conceeded **' then
    Single10AwayPlayerName.Value := '';
end;
// Player 11
procedure TSSingles.DBEdit22DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit22DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit22.Text);
  DBEdit22.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Single11AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit22.Text);
end;

procedure TSSingles.DBEdit22DblClick(Sender: TObject);
begin
  AddToList2(DBEdit22.Text);
  DBEdit22.Text := '';
  Single11AwayPlayerName.Value := '';
end;
// Player 12
procedure TSSingles.DBEdit24DragOver(Sender, Source: TObject; X, Y: Integer;
  State: TDragState; var Accept: Boolean);
begin
  Accept := False;
  if ListBox2.ItemIndex >= 0 then
    Accept := True;
end;

procedure TSSingles.DBEdit24DragDrop(Sender, Source: TObject; X, Y: Integer);
begin
  AddToList2(DBEdit24.Text);
 DBEdit24.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
  Single12AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
  RemoveFromList2(DBEdit24.Text);
end;

procedure TSSingles.DBEdit24DblClick(Sender: TObject);
begin
  AddToList2(DBEdit24.Text);
  DBEdit24.Text := '';
  Single12AwayPlayerName.Value := '';
end;
// End of Away Player Drag and Drop

procedure TSSingles.BitBtn1Click(Sender: TObject);
begin
  if LeagueQueryNoSingles.Value > 0 then
    Single1.Post;
  if LeagueQueryNoSingles.Value > 1 then
    Single2.Post;
  if LeagueQueryNoSingles.Value > 2 then
    Single3.Post;
  if LeagueQueryNoSingles.Value > 3 then
    Single4.Post;
  if LeagueQueryNoSingles.Value > 4 then
    Single5.Post;
  if LeagueQueryNoSingles.Value > 5 then
    Single6.Post;
  if LeagueQueryNoSingles.Value > 6 then
    Single7.Post;
  if LeagueQueryNoSingles.Value > 7 then
    Single8.Post;
  if LeagueQueryNoSingles.Value > 8 then
    Single9.Post;
  if LeagueQueryNoSingles.Value > 9 then
    Single10.Post;
  if LeagueQueryNoSingles.Value > 10 then
    Single11.Post;
  if LeagueQueryNoSingles.Value > 11 then
    Single12.Post;
  Close;  
end;

procedure TSSingles.SpeedButton1Click(Sender: TObject);
var sendstring: String;
var i: Integer;
begin
  sendstring := hometeam;
  PlayersForm.InsertNewPlayer(sendstring);
  if sendstring = '' then abort;
  for i := 1 to LeagueQueryMaxSingles.Value do
    ListBox1.Items.Add(sendstring);
end;

procedure TSSingles.SpeedButton2Click(Sender: TObject);
var sendstring: String;
var i: Integer;
begin
  sendstring := awayteam;
  PlayersForm.InsertNewPlayer(sendstring);
  if sendstring = '' then abort;
  for i := 1 to LeagueQueryMaxSingles.Value do
    ListBox2.Items.Add(sendstring);
end;

procedure TSSingles.ListBox2DblClick(Sender: TObject);
begin
  if LeagueQueryNoSingles.Value < 1 then abort;
  if DBEdit2.Text = '' then
  begin
    DBEdit2.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
    Single1AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
    RemoveFromList2(DBEdit2.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 2 then abort;
  if DBEdit4.Text = '' then
  begin
    DBEdit4.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
    Single2AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
    RemoveFromList2(DBEdit4.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 3 then abort;
  if DBEdit6.Text = '' then
  begin
    DBEdit6.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
    Single3AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
    RemoveFromList2(DBEdit6.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 4 then abort;
  if DBEdit8.Text = '' then
  begin
    DBEdit8.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
    Single4AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
    RemoveFromList2(DBEdit8.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 5 then abort;
  if DBEdit10.Text = '' then
  begin
    DBEdit10.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
    Single5AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
    RemoveFromList2(DBEdit10.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 6 then abort;
  if DBEdit12.Text = '' then
  begin
    DBEdit12.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
    Single6AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
    RemoveFromList2(DBEdit12.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 7 then abort;
  if DBEdit14.Text = '' then
  begin
    DBEdit14.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
    Single7AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
    RemoveFromList2(DBEdit14.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 8 then abort;
  if DBEdit16.Text = '' then
  begin
    DBEdit16.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
    Single8AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
    RemoveFromList2(DBEdit16.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 9 then abort;
  if DBEdit18.Text = '' then
  begin
    DBEdit18.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
    Single9AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
    RemoveFromList2(DBEdit18.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 10 then abort;
  if DBEdit20.Text = '' then
  begin
    DBEdit20.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
    Single10AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
    RemoveFromList2(DBEdit20.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 11 then abort;
  if DBEdit22.Text = '' then
  begin
    DBEdit22.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
    Single11AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
    RemoveFromList2(DBEdit22.Text);
    Abort;
  end;
  if LeagueQueryNoSingles.Value < 12 then abort;
  if DBEdit24.Text = '' then
  begin
    DBEdit24.Text := ListBox2.Items.Strings[ListBox2.ItemIndex];
    Single12AwayPlayerName.Value := ListBox2.Items.Strings[ListBox2.ItemIndex];
    RemoveFromList2(DBEdit24.Text);
    Abort;
  end;
end;

procedure TSSingles.ListBox2Exit(Sender: TObject);
begin
  ListBox2.ItemIndex := -1;
end;

procedure TSSingles.BitBtn2Click(Sender: TObject);
begin
  Close;
end;

end.
