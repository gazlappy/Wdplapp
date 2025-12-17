unit Matchbyd;

interface

uses SysUtils, WinTypes, WinProcs, Classes, Graphics, Forms, Controls, Buttons,
  StdCtrls, DB, DBTables, Grids, Dialogs, DBGrids;

type
  TMatchByDateForm = class(TForm)
    OKBtn: TBitBtn;
    GroupBox1: TGroupBox;
    DateBox: TEdit;
    GroupBox2: TGroupBox;
    DBGrid1: TDBGrid;
    MatchList: TQuery;
    MatchSource: TDataSource;
    MatchListMatchNo: TFloatField;
    MatchListHomeTeam: TStringField;
    MatchListAwayTeam: TStringField;
    MatchListMatchDate: TDateField;
    BitBtn1: TBitBtn;
    procedure DateBoxExit(Sender: TObject);
    procedure DBGrid1DblClick(Sender: TObject);
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  MatchByDateForm: TMatchByDateForm;

implementation

uses match, NMatch, Main, Matchbyt;

{$R *.DFM}

procedure TMatchByDateForm.DateBoxExit(Sender: TObject);
var Subs: TDateTime;
begin
  MatchList.Close;
  Subs := StrToDate(Datebox.Text);
  MatchList.Params[0].AsDateTime := Subs;
  MatchList.Open;
  if MatchList.EOF then
  begin
    MessageDlg('No Matches on selected date',mtError,[mbOk],0);
    DateBox.SetFocus;
  end
  else
    GroupBox2.Caption := 'Matches on ' + Datebox.Text;
end;

procedure TMatchByDateForm.DBGrid1DblClick(Sender: TObject);
begin
  MatchByDateForm.Enabled := False;
  Application.CreateForm(TNMForm, NMForm);
  NMForm.Edit(DBGrid1.Fields[0].Value);
//  MatchList.Close;
//  MatchList.Open;
end;

procedure TMatchByDateForm.FormClose(Sender: TObject;
  var Action: TCloseAction);
begin
  Action := caFree;
end;

end.
