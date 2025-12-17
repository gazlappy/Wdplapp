unit Venue;

interface

uses WinTypes, WinProcs, Classes, Graphics, Forms, Controls, Buttons,
  StdCtrls, DB, DBTables, ExtCtrls, DBCtrls, Grids, DBGrids,
  Dialogs, SysUtils;

type
  TVenuesForm = class(TForm)
    BitBtn1: TBitBtn;
    Button1: TButton;
    Delete: TButton;
    DeleteQuery: TQuery;
    DBGrid1: TDBGrid;
    procedure BitBtn1Click(Sender: TObject);
    procedure DeleteClick(Sender: TObject);
    procedure Button1Click(Sender: TObject);
    procedure DBGrid1Exit(Sender: TObject);
    procedure FormActivate(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  VenuesForm: TVenuesForm;

implementation

uses venins, Main, datamodule;
{$R *.DFM}

procedure TVenuesForm.BitBtn1Click(Sender: TObject);
begin
  DM1.Venue.Post;
  ModalResult := mrOK;
end;

procedure TVenuesForm.DeleteClick(Sender: TObject);
var
  Key: string;
begin
  Key := DBGrid1.Fields[0].AsString;
  if MessageDlg(Format('Delete "%S" from the Venue table?', [Key]),
    mtConfirmation, mbOKCancel, 0) = mrOK then
  begin
    DeleteQuery.Prepare;
    DeleteQuery.Params[0].AsString := Key;
    DeleteQuery.ExecSQL;
    DM1.Venue.Refresh;
    VenuesForm.Caption := 'Venues (' + IntToStr(DM1.Venue.RecordCount) + ')';
  end;

end;

procedure TVenuesForm.Button1Click(Sender: TObject);
begin
  VenuesForm.Enabled := False;
  VenueInsert.ShowModal;
  VenuesForm.Caption := 'Venues (' + IntToStr(DM1.Venue.RecordCount) + ')';
end;

procedure TVenuesForm.DBGrid1Exit(Sender: TObject);
begin
  if DM1.Venue.State in [dsEdit,dsInsert] then
    DM1.Venue.Post;
end;

procedure TVenuesForm.FormActivate(Sender: TObject);
begin
  VenuesForm.Caption := 'Venues (' + IntToStr(DM1.Venue.RecordCount) + ')';
end;

end.
