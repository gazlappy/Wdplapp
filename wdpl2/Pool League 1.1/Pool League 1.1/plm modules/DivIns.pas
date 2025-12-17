unit Divins;

interface

uses WinTypes, WinProcs, Classes, Graphics, Forms, Controls, Buttons,
  StdCtrls, ExtCtrls, DB, DBTables;

type
  TDivisionInsert = class(TForm)
    OKBtn: TBitBtn;
    CancelBtn: TBitBtn;
    Bevel1: TBevel;
    Label1: TLabel;
    Label2: TLabel;
    Edit1: TEdit;
    Edit2: TEdit;
    procedure FormCreate(Sender: TObject);
    procedure OKBtnClick(Sender: TObject);
    procedure CancelBtnClick(Sender: TObject);
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
  private
    { Private declarations }
  public
    procedure Clear;
    { Public declarations }
  end;

var
  DivisionInsert: TDivisionInsert;

implementation

uses Division, datamodule;

{$R *.DFM}

procedure TDivisionInsert.FormCreate(Sender: TObject);
begin
  DivisionInsert.Clear;
end;

procedure TDivisionInsert.Clear;
begin
  ActiveControl := Edit1;
  Edit1.Clear;
  Edit2.Clear;
end;

procedure TDivisionInsert.OKBtnClick(Sender: TObject);
begin
  if Edit1.Text <> '' then
  begin
    DM1.Division.Insert;
    DM1.DivisionAbbreviated.Value := Edit1.Text;
    DM1.DivisionFullDivisionName.Value := Edit2.Text;
    DM1.Division.Post;
  end;
  ModalResult := mrOK;
end;

procedure TDivisionInsert.CancelBtnClick(Sender: TObject);
begin
  ModalResult := mrCancel;
end;

procedure TDivisionInsert.FormClose(Sender: TObject;
  var Action: TCloseAction);
begin
  DivisionsForm.Enabled := True;
  DM1.Division.Refresh;
end;

end.
