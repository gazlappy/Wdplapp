unit newleague;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls, 
  Buttons, ExtCtrls, Dialogs;

type
  TNewLeagueDlg = class(TForm)
    Label1: TLabel;
    Edit1: TEdit;
    GroupBox1: TGroupBox;
    Label2: TLabel;
    ListBox1: TListBox;
    Label3: TLabel;
    Edit2: TEdit;
    BitBtn1: TBitBtn;
    BitBtn2: TBitBtn;
    procedure OKBtnClick(Sender: TObject);
    procedure CancelBtnClick(Sender: TObject);
    procedure FormShow(Sender: TObject);
  private
    { Private declarations }
  public
    UniRefs: Array of Integer;
    NewLeague: Boolean;
    procedure MoveRecords(NewLeague: Boolean);
{ Public declarations }
  end;

var
  NewLeagueDlg: TNewLeagueDlg;

implementation

uses datamodule2, datamodule, Main, openleague;

{$R *.DFM}

procedure TNewLeagueDlg.OKBtnClick(Sender: TObject);
begin
  NewLeague := True;
  MoveRecords(NewLeague);
end;

procedure TNewLeagueDlg.MoveRecords(NewLeague: Boolean);
var UniRef, Template: Integer;
var dummy, outtext: String;
begin
//New League
  if NewLeague then
    Template := UniRefs[ListBox1.ItemIndex]
  else
    Template := OpenLeagueDlg.UniRefs[OpenLeagueDlg.ListBox1.ItemIndex];
//Copy all records out to the archive files
//Move the league record first, capturing the incremental
//key for use with all other records.
  DM2.BM_League.Execute;
//Above BM creates the UniRef - find this value
  DM2.MaxUniRef.Close;
  DM2.MaxUniRef.Open;
  if DM2.MaxUniRef.Fields[0].IsNull then
  begin
    dummy := TimeToStr(Time);
    dummy := Copy(dummy,1,2) + Copy(dummy,4,2) + Copy(dummy,7,2);
    UniRef := StrToInt(dummy);
  end
  else
    UniRef := DM2.MaxUniRef.Fields[0].AsInteger;
//
// ----- Move all current data out to the a_ files -----
// Move divisions
  DM2.BM_Files.Source := DM1.Division;
  DM2.BM_Files.Destination := DM2.a_Division;
  DM2.BM_Files.Execute;
  outtext := 'Exported ' + IntToStr(DM2.BM_Files.MovedCount) + ' Divisions';
  DM2.CopySource.Close;
  DM2.CopySource.SQL.Clear;
  DM2.CopySource.SQL.Add('Select * from a_Division');
  DM2.CopySource.Open;
  DM2.CopySource.First;
  while not DM2.CopySource.EOF do
  begin
    DM2.CopySource.Edit;
    if DM2.CopySourceUniRef.Value < 1 then
      DM2.CopySourceUniRef.Value := UniRef;
    DM2.CopySource.Post;
    DM2.CopySource.Next;
  end;
// Move Venues
  DM2.BM_Files.Source := DM1.Venue;
  DM2.BM_Files.Destination := DM2.a_Venue;
  DM2.BM_Files.Execute;
  outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Venues';
  DM2.CopySource.Close;
  DM2.CopySource.SQL.Clear;
  DM2.CopySource.SQL.Add('Select * from a_Venue');
  DM2.CopySource.Open;
  DM2.CopySource.First;
  while not DM2.CopySource.EOF do
  begin
    DM2.CopySource.Edit;
    if DM2.CopySourceUniRef.Value < 1 then
      DM2.CopySourceUniRef.Value := UniRef;
    DM2.CopySource.Post;
    DM2.CopySource.Next;
  end;
// Move Teams
  DM2.BM_Files.Source := DM1.Team;
  DM2.BM_Files.Destination := DM2.a_Team;
  DM2.BM_Files.Execute;
  outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Teams';
  DM2.CopySource.Close;
  DM2.CopySource.SQL.Clear;
  DM2.CopySource.SQL.Add('Select * from a_Team');
  DM2.CopySource.Open;
  DM2.CopySource.First;
  while not DM2.CopySource.EOF do
  begin
    DM2.CopySource.Edit;
    if DM2.CopySourceUniRef.Value < 1 then
      DM2.CopySourceUniRef.Value := UniRef;
    DM2.CopySource.Post;
    DM2.CopySource.Next;
  end;
// Move Team_1
  DM2.BM_Files.Source := DM1.Team_1;
  DM2.BM_Files.Destination := DM2.a_Team_1;
  DM2.BM_Files.Execute;
  outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Team Fines';
  DM2.CopySource.Close;
  DM2.CopySource.SQL.Clear;
  DM2.CopySource.SQL.Add('Select * from a_Team_1');
  DM2.CopySource.Open;
  DM2.CopySource.First;
  while not DM2.CopySource.EOF do
  begin
    DM2.CopySource.Edit;
    if DM2.CopySourceUniRef.Value < 1 then
      DM2.CopySourceUniRef.Value := UniRef;
    DM2.CopySource.Post;
    DM2.CopySource.Next;
  end;
// Move Players
  DM2.BM_Files.Source := DM1.AllPlayerLookUp;
  DM2.BM_Files.Destination := DM2.a_Player;
  DM2.BM_Files.Execute;
  outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Players';
  DM2.CopySource.Close;
  DM2.CopySource.SQL.Clear;
  DM2.CopySource.SQL.Add('Select * from a_Player');
  DM2.CopySource.Open;
  DM2.CopySource.First;
  while not DM2.CopySource.EOF do
  begin
    DM2.CopySource.Edit;
    if DM2.CopySourceUniRef.Value < 1 then
      DM2.CopySourceUniRef.Value := UniRef;
    DM2.CopySource.Post;
    DM2.CopySource.Next;
  end;
// Move Pairs
  DM2.BM_Files.Source := DM1.Pair;
  DM2.BM_Files.Destination := DM2.a_Pair;
  DM2.BM_Files.Execute;
  outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Pairs';
  DM2.CopySource.Close;
  DM2.CopySource.SQL.Clear;
  DM2.CopySource.SQL.Add('Select * from a_Pair');
  DM2.CopySource.Open;
  DM2.CopySource.First;
  while not DM2.CopySource.EOF do
  begin
    DM2.CopySource.Edit;
    if DM2.CopySourceUniRef.Value < 1 then
      DM2.CopySourceUniRef.Value := UniRef;
    DM2.CopySource.Post;
    DM2.CopySource.Next;
  end;
// Move Matches
  DM2.BM_Files.Source := DM1.Match;
  DM2.BM_Files.Destination := DM2.a_Match;
  DM2.BM_Files.Execute;
  outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Matches';
  DM2.CopySource.Close;
  DM2.CopySource.SQL.Clear;
  DM2.CopySource.SQL.Add('Select * from a_Match');
  DM2.CopySource.Open;
  DM2.CopySource.First;
  while not DM2.CopySource.EOF do
  begin
    DM2.CopySource.Edit;
    if DM2.CopySourceUniRef.Value < 1 then
      DM2.CopySourceUniRef.Value := UniRef;
    DM2.CopySource.Post;
    DM2.CopySource.Next;
  end;
// Move Singles
  DM2.BM_Files.Source := DM1.SingleLookUp;
  DM2.BM_Files.Destination := DM2.a_Single;
  DM2.BM_Files.Execute;
  outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Singles';
  DM2.CopySource.Close;
  DM2.CopySource.SQL.Clear;
  DM2.CopySource.SQL.Add('Select * from a_Single');
  DM2.CopySource.Open;
  DM2.CopySource.First;
  while not DM2.CopySource.EOF do
  begin
    DM2.CopySource.Edit;
    if DM2.CopySourceUniRef.Value < 1 then
      DM2.CopySourceUniRef.Value := UniRef;
    DM2.CopySource.Post;
    DM2.CopySource.Next;
  end;
// Move Doubles
  DM2.BM_Files.Source := DM1.DoubleLookUp;
  DM2.BM_Files.Destination := DM2.a_Dbls;
  DM2.BM_Files.Execute;
  outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Doubles';
  DM2.CopySource.Close;
  DM2.CopySource.SQL.Clear;
  DM2.CopySource.SQL.Add('Select * from a_Dbls');
  DM2.CopySource.Open;
  DM2.CopySource.First;
  while not DM2.CopySource.EOF do
  begin
    DM2.CopySource.Edit;
    if DM2.CopySourceUniRef.Value < 1 then
      DM2.CopySourceUniRef.Value := UniRef;
    DM2.CopySource.Post;
    DM2.CopySource.Next;
  end;
//
// ----- Delete all data on the live files -----
// But leave Division, Venue, Team, Team_1, Player if new league is
// based on current active one
//
  if NewLeague and (Template = -1) then
// don't remove
  else
  begin
    DM2.DeleteStandingData.SQL.Clear;
    DM2.DeleteStandingData.SQL.Add('Delete from League');
    DM2.DeleteStandingData.ExecSQL;
    DM2.DeleteStandingData.SQL.Clear;
    DM2.DeleteStandingData.SQL.Add('Delete from Division');
    DM2.DeleteStandingData.ExecSQL;
    DM2.DeleteStandingData.SQL.Clear;
    DM2.DeleteStandingData.SQL.Add('Delete from Venue');
    DM2.DeleteStandingData.ExecSQL;
    DM2.DeleteStandingData.SQL.Clear;
    DM2.DeleteStandingData.SQL.Add('Delete from Team');
    DM2.DeleteStandingData.ExecSQL;
    DM2.DeleteStandingData.SQL.Clear;
    DM2.DeleteStandingData.SQL.Add('Delete from Team_1');
    DM2.DeleteStandingData.ExecSQL;
    DM2.DeleteStandingData.SQL.Clear;
    DM2.DeleteStandingData.SQL.Add('Delete from Player');
    DM2.DeleteStandingData.ExecSQL;
  end;
// Always delete all match, single and double related info
  DM2.DeleteStandingData.SQL.Clear;
  DM2.DeleteStandingData.SQL.Add('Delete from Match');
  DM2.DeleteStandingData.ExecSQL;
  DM2.DeleteStandingData.SQL.Clear;
  DM2.DeleteStandingData.SQL.Add('Delete from Single');
  DM2.DeleteStandingData.ExecSQL;
  DM2.DeleteStandingData.SQL.Clear;
  DM2.DeleteStandingData.SQL.Add('Delete from Dbls');
  DM2.DeleteStandingData.ExecSQL;
  DM2.DeleteStandingData.SQL.Clear;
  DM2.DeleteStandingData.SQL.Add('Delete from Pair');
  DM2.DeleteStandingData.ExecSQL;
  DM2.DeleteStandingData.SQL.Clear;
  DM2.DeleteStandingData.SQL.Add('Delete from Daterate');
  DM2.DeleteStandingData.ExecSQL;
  DM2.DeleteStandingData.SQL.Clear;
  DM2.DeleteStandingData.SQL.Add('Delete from Dblrate');
  DM2.DeleteStandingData.ExecSQL;
  MessageDlg(outtext, mtInformation, [mbOK], 0);
//
// ----- Move template data back in -----
// If current active league not used as the template for the
// new league, move the appropriate records back in from a_ files
// League
  if Template > 0 then
  begin
    DM2.CopySource.Close;
    DM2.CopySource.SQL.Clear;
    DM2.CopySource.SQL.Add('Select * from a_League');
    DM2.CopySource.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.BM_Files.Source := DM2.CopySource;
    DM2.BM_Files.Destination := DM1.League;
    DM2.BM_Files.Execute;
    outtext := 'Imported ' + IntToStr(DM2.BM_Files.MovedCount) + ' League';
// Division
    DM2.CopySource.Close;
    DM2.CopySource.SQL.Clear;
    DM2.CopySource.SQL.Add('Select * from a_Division');
    DM2.CopySource.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.BM_Files.Source := DM2.CopySource;
    DM2.BM_Files.Destination := DM1.Division;
    DM2.BM_Files.Execute;
    outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Divisions';
// Venue
    DM2.CopySource.Close;
    DM2.CopySource.SQL.Clear;
    DM2.CopySource.SQL.Add('Select * from a_Venue');
    DM2.CopySource.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.BM_Files.Source := DM2.CopySource;
    DM2.BM_Files.Destination := DM1.Venue;
    DM2.BM_Files.Execute;
    outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Venues';
// Team (Team_1 not required - fines data only)
    DM2.CopySource.Close;
    DM2.CopySource.SQL.Clear;
    DM2.CopySource.SQL.Add('Select * from a_Team');
    DM2.CopySource.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.BM_Files.Source := DM2.CopySource;
    DM2.BM_Files.Destination := DM1.Team;
    DM2.BM_Files.Execute;
    outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Teams';
// Players
    DM2.CopySource.Close;
    DM2.CopySource.SQL.Clear;
    DM2.CopySource.SQL.Add('Select * from a_Player');
    DM2.CopySource.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.BM_Files.Source := DM2.CopySource;
    DM2.BM_Files.Destination := DM1.Player;
    DM2.BM_Files.Execute;
    outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Players';
  end;
//
// ----- Move other files back in if it's an 'open' -----
//
  if not NewLeague then
  begin
// Team_1
    DM2.CopySource.Close;
    DM2.CopySource.SQL.Clear;
    DM2.CopySource.SQL.Add('Select * from a_Team_1');
    DM2.CopySource.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.BM_Files.Source := DM2.CopySource;
    DM2.BM_Files.Destination := DM1.Team_1;
    DM2.BM_Files.Execute;
    outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Team Fines';
// Matches
    DM2.CopySource.Close;
    DM2.CopySource.SQL.Clear;
    DM2.CopySource.SQL.Add('Select * from a_Match');
    DM2.CopySource.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.BM_Files.Source := DM2.CopySource;
    DM2.BM_Files.Destination := DM1.Match;
    DM2.BM_Files.Execute;
    outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Matches';
// Singles
    DM2.CopySource.Close;
    DM2.CopySource.SQL.Clear;
    DM2.CopySource.SQL.Add('Select * from a_Single');
    DM2.CopySource.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.BM_Files.Source := DM2.CopySource;
    DM2.BM_Files.Destination := DM1.Single;
    DM2.BM_Files.Execute;
    outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Singles';
// Doubles
    DM2.CopySource.Close;
    DM2.CopySource.SQL.Clear;
    DM2.CopySource.SQL.Add('Select * from a_Dbls');
    DM2.CopySource.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.BM_Files.Source := DM2.CopySource;
    DM2.BM_Files.Destination := DM1.Dbls;
    DM2.BM_Files.Execute;
    outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Doubles';
// Pairs
    DM2.CopySource.Close;
    DM2.CopySource.SQL.Clear;
    DM2.CopySource.SQL.Add('Select * from a_Pair');
    DM2.CopySource.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.BM_Files.Source := DM2.CopySource;
    DM2.BM_Files.Destination := DM1.Pair;
    DM2.BM_Files.Execute;
    outtext := outtext + ', ' + IntToStr(DM2.BM_Files.MovedCount) + ' Pairs';
  end;
  MessageDlg(outtext, mtInformation, [mbOK], 0);
//
//
// ----- Delete all archive data if it's an open -----
//
  if not NewLeague then
  begin
    DM2.DeleteArchiveData.SQL.Clear;
    DM2.DeleteArchiveData.SQL.Add('Delete from a_League');
    DM2.DeleteArchiveData.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.DeleteArchiveData.ExecSQL;
    DM2.DeleteArchiveData.SQL.Clear;
    DM2.DeleteArchiveData.SQL.Add('Delete from a_Division');
    DM2.DeleteArchiveData.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.DeleteArchiveData.ExecSQL;
    DM2.DeleteArchiveData.SQL.Clear;
    DM2.DeleteArchiveData.SQL.Add('Delete from a_Venue');
    DM2.DeleteArchiveData.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.DeleteArchiveData.ExecSQL;
    DM2.DeleteArchiveData.SQL.Clear;
    DM2.DeleteArchiveData.SQL.Add('Delete from a_Team');
    DM2.DeleteArchiveData.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.DeleteArchiveData.ExecSQL;
    DM2.DeleteArchiveData.SQL.Clear;
    DM2.DeleteArchiveData.SQL.Add('Delete from a_Team_1');
    DM2.DeleteArchiveData.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.DeleteArchiveData.ExecSQL;
    DM2.DeleteArchiveData.SQL.Clear;
    DM2.DeleteArchiveData.SQL.Add('Delete from a_Player');
    DM2.DeleteArchiveData.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.DeleteArchiveData.ExecSQL;
    DM2.DeleteArchiveData.SQL.Clear;
    DM2.DeleteArchiveData.SQL.Add('Delete from a_Match');
    DM2.DeleteArchiveData.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.DeleteArchiveData.ExecSQL;
    DM2.DeleteArchiveData.SQL.Clear;
    DM2.DeleteArchiveData.SQL.Add('Delete from a_Single');
    DM2.DeleteArchiveData.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.DeleteArchiveData.ExecSQL;
    DM2.DeleteArchiveData.SQL.Clear;
    DM2.DeleteArchiveData.SQL.Add('Delete from a_Dbls');
    DM2.DeleteArchiveData.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.DeleteArchiveData.ExecSQL;
    DM2.DeleteArchiveData.SQL.Clear;
    DM2.DeleteArchiveData.SQL.Add('Delete from a_Pair');
    DM2.DeleteArchiveData.SQL.Add('where UniRef = ' + IntToStr(Template));
    DM2.DeleteArchiveData.ExecSQL;
  end;
//
// Name the new league
//
  if NewLeague then
  begin
    DM1.League.Open;
    if ListBox1.ItemIndex = 0 then
    begin
      DM1.League.Insert;
      DM1.LeagueLeagueName.Value := Edit1.Text;
      DM1.LeagueSeason.Value := Edit2.Text;
    end
    else
    begin
      DM1.League.First;
      DM1.League.Edit;
      DM1.LeagueLeagueName.Value := Edit1.Text;
      DM1.LeagueSeason.Value := Edit2.Text;
    end;
    DM1.League.Post;
    DM1.League.Edit;
  end;
  DM1.Match.Open;
  DM1.Single.Open;
  DM1.Dbls.Open;
  DM1.Double.Open;
  DM1.Division.Open;
  DM1.Venue.Open;
  DM1.Team.Open;
  DM1.Team_1.Open;
  DM1.Player.Open;
  DM1.Pair.Open;
  DM1.AllPlayerLookUp.Open;
  DM1.SingleLookUp.Open;
  DM1.DoubleLookUp.Open;
  DM1.Match.Refresh;
  DM1.Single.Refresh;
  DM1.Dbls.Refresh;
  DM1.Double.Refresh;
  DM1.Division.Refresh;
  DM1.Venue.Refresh;
  DM1.Team.Refresh;
  DM1.Team_1.Refresh;
  DM1.Player.Refresh;
  DM1.Pair.Refresh;
  DM1.AllPlayerLookUp.Refresh;
  DM1.SingleLookUp.Refresh;
  DM1.DoubleLookUp.Refresh;
  Form1.DoubleGrid.Enabled := False;
  Form1.SingleGrid.Enabled := False;
  DM1.League.First;
  if not DM1.League.EOF then
  begin
    if DM1.LeagueNoDoubles.Value = 0 then
      Form1.DoubleGrid.Enabled := False
    else
      Form1.DoubleGrid.Enabled := True;
    if DM1.LeagueNoSingles.Value = 0 then
      Form1.SingleGrid.Enabled := False
    else
      Form1.SingleGrid.Enabled := True;
    Form1.StatusBar1.Panels[0].Text := DM1.LeagueLeagueName.Value;
    Form1.StatusBar1.Panels[1].Text := DM1.LeagueSeason.Value;
    Form1.PopulateTeams;
    Form1.Refresh;
  end
  else
  begin
    Form1.StatusBar1.Panels[0].Text := 'Unknown';
    Form1.Database1.Enabled := True;
  end;
end;

procedure TNewLeagueDlg.CancelBtnClick(Sender: TObject);
begin
  Close;
end;

procedure TNewLeagueDlg.FormShow(Sender: TObject);
var dummy: Integer;
var Present: TDateTime;
var Year, Month, Day, Hour, Min, Sec, MSec: Word;
begin
  Present:= Now;
  DecodeDate(Present, Year, Month, Day);
  Edit2.Text := IntToStr(Year);
  SetLength(UniRefs,999);
  ListBox1.Items.Clear;
  ListBox1.Items.Add('None');
  UniRefs[0] := -2;
  ListBox1.Items.Add(DM1.LeagueLeagueName.Value + ' ' + DM1.LeagueSeason.Value);
  UniRefs[1] := -1;
  DM2.a_League.Open;
  DM2.a_League.First;
  dummy := 2;
  while not DM2.a_League.EOF do
  begin
    ListBox1.Items.Add(DM2.a_LeagueLeagueName.Value + ' ' + DM2.a_LeagueSeason.Value);
    UniRefs[dummy] := DM2.a_LeagueUniRef.Value;
    DM2.a_League.Next;
    dummy := dummy + 1;
  end;
  ListBox1.ItemIndex := 0;
end;

end.
