unit Teaml;

interface

uses WinTypes, WinProcs, Classes, Graphics, Forms, Controls, Buttons,
  StdCtrls, DB, DBTables, Grids, DBGrids, ExtCtrls,
  Dialogs, SysUtils;

type
  TTeamList = class(TForm)
    OKBtn: TBitBtn;
    DBGrid1: TDBGrid;
    Button1: TButton;
    Delete: TButton;
    Amend: TButton;
    DeleteQuery: TQuery;
    procedure FormCreate(Sender: TObject);
    procedure OKBtnClick(Sender: TObject);
    procedure DeleteClick(Sender: TObject);
    procedure Button1Click(Sender: TObject);
    procedure AmendClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  TeamList: TTeamList;

implementation

uses team, Main, datamodule;
{$R *.DFM}

procedure TTeamList.FormCreate(Sender: TObject);
begin
  TeamList.Caption := 'Teams (' + IntToStr(DM1.Team.RecordCount) + ')';
  TeamList.Refresh;
end;

procedure TTeamList.OKBtnClick(Sender: TObject);
begin
  ModalResult := mrOK;
end;

procedure TTeamList.DeleteClick(Sender: TObject);
var
  Key: string;
begin
  Key := DBGrid1.Fields[0].AsString;
  if MessageDlg(Format('Delete "%S" from the Team table?', [Key]),
    mtConfirmation, mbOKCancel, 0) = mrOK then
  begin
    DeleteQuery.Prepare;
    DeleteQuery.Params[0].AsString := Key;
    DeleteQuery.ExecSQL;
    DM1.Team.Refresh;
    TeamList.Caption := 'Teams (' + IntToStr(DM1.Team.RecordCount) + ')';
  end;
end;

procedure TTeamList.Button1Click(Sender: TObject);
begin
  TeamList.Enabled := False;
  TeamsForm.Enter;
  TeamList.Caption := 'Teams (' + IntToStr(DM1.Team.RecordCount) + ')';
end;

procedure TTeamList.AmendClick(Sender: TObject);
begin
  TeamList.Enabled := False;
  TeamsForm.Edit(DM1.TeamItem_id.Value);
end;


end.
