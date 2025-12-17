unit Matchbyt;

interface

uses WinTypes, WinProcs, Classes, Graphics, Forms, Controls, Buttons,
  StdCtrls, DB, DBTables, ExtCtrls, Grids, DBGrids, Dialogs, SysUtils;

type
  TMatchByTeamForm = class(TForm)
    CancelBtn: TBitBtn;
    TeamList: TQuery;
    TeamSource: TDataSource;
    MatchList: TQuery;
    MatchSource: TDataSource;
    MatchListMatchNo: TFloatField;
    MatchListHomeTeam: TStringField;
    MatchListAwayTeam: TStringField;
    MatchListMatchDate: TDateField;
    GroupBox1: TGroupBox;
    GroupBox2: TGroupBox;
    DBGrid1: TDBGrid;
    TeamCombo: TComboBox;
    BitBtn1: TBitBtn;
    procedure TeamComboEnter(Sender: TObject);
    procedure TeamComboChange(Sender: TObject);
    procedure DBGrid1DblClick(Sender: TObject);
    procedure FormShow(Sender: TObject);
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
    procedure CancelBtnClick(Sender: TObject);
    procedure FormActivate(Sender: TObject);
  private
    procedure RefreshGrid;
    { Private declarations }
  public
    { Public declarations }
  end;

var
  MatchByTeamForm: TMatchByTeamForm;

implementation

uses NMatch, Main;

{$R *.DFM}

procedure TMatchByTeamForm.TeamComboEnter(Sender: TObject);
begin
  TeamList.Close;
  TeamList.Open;
  TeamList.First;
  TeamCombo.Clear;
  while not TeamList.EOF do
  begin
    TeamCombo.Items.Add(TeamList.Fields[0].AsString);
    TeamList.Next;
  end;
end;

procedure TMatchByTeamForm.TeamComboChange(Sender: TObject);
begin
  RefreshGrid;
  if MatchList.EOF then
    ShowMessage('No Matches for '+TeamCombo.Text);
end;

procedure TMatchByTeamForm.RefreshGrid;
begin
  BitBtn1.Enabled := False;
  GroupBox2.Enabled := False;
  MatchList.Params[0].AsString := TeamCombo.Text;
  MatchList.Params[1].AsString := TeamCombo.Text;
  MatchList.Close;
  MatchList.Open;
  if not MatchList.EOF then
  begin
    GroupBox2.Enabled := True;
    BitBtn1.Enabled := True;
    GroupBox2.Caption := 'Matches involving ' + TeamCombo.Text;
  end;
end;

procedure TMatchByTeamForm.DBGrid1DblClick(Sender: TObject);
begin
  MatchByTeamForm.Enabled := False;
  Application.CreateForm(TNMForm, NMForm);
  NMForm.Edit(DBGrid1.Fields[0].Value);
  MatchList.Close;
  MatchList.Open;
end;

procedure TMatchByTeamForm.FormShow(Sender: TObject);
begin
  GroupBox2.Enabled := False;
  BitBtn1.Enabled := False;
end;

procedure TMatchByTeamForm.FormClose(Sender: TObject;
  var Action: TCloseAction);
begin
  MatchList.Close;
  TeamCombo.Clear;
  Action := caFree;
end;

procedure TMatchByTeamForm.CancelBtnClick(Sender: TObject);
begin
  Close;
end;

procedure TMatchByTeamForm.FormActivate(Sender: TObject);
begin
  RefreshGrid;
end;

end.
