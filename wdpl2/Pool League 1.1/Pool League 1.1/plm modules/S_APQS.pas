unit S_APQS;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls,
  Buttons, ExtCtrls, Grids, DBGrids;

type
  TS_APQSDlg = class(TForm)
    DBGrid1: TDBGrid;
    HomeButton: TBitBtn;
    AwayButton: TBitBtn;
    TrueButton: TBitBtn;
    FalseButton: TBitBtn;
    procedure DBGrid1DblClick(Sender: TObject);
    procedure HomeButtonClick(Sender: TObject);
    procedure AwayButtonClick(Sender: TObject);
    procedure TrueButtonClick(Sender: TObject);
    procedure FalseButtonClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  S_APQSDlg: TS_APQSDlg;

implementation

uses datamodule, Main;

{$R *.DFM}

procedure TS_APQSDlg.DBGrid1DblClick(Sender: TObject);
begin
  if Form1.SingleGrid.SelectedIndex = 3 then
  begin
    DM1.Single.Edit;
    Form1.SingleGrid.SelectedIndex := Form1.SingleGrid.SelectedIndex + 1;
    Form1.SingleGrid.SelectedField.Value := DM1.AwayPlayerLookUpPlayerNo.Value;
  end;
  Form1.SetFocus;
end;

procedure TS_APQSDlg.HomeButtonClick(Sender: TObject);
begin
  if Form1.SingleGrid.SelectedIndex = 5 then
  begin
    DM1.Single.Edit;
    Form1.SingleGrid.SelectedField.Value := 'Home';
    Form1.SingleGrid.SelectedIndex := Form1.SingleGrid.SelectedIndex + 1;
  end;
  Form1.SetFocus;
end;

procedure TS_APQSDlg.AwayButtonClick(Sender: TObject);
begin
  if Form1.SingleGrid.SelectedIndex = 5 then
  begin
    DM1.Single.Edit;
    Form1.SingleGrid.SelectedField.Value := 'Away';
    Form1.SingleGrid.SelectedIndex := Form1.SingleGrid.SelectedIndex + 1;
  end;
  Form1.SetFocus;
end;

procedure TS_APQSDlg.TrueButtonClick(Sender: TObject);
begin
  if Form1.SingleGrid.SelectedIndex = 6 then
  begin
    DM1.Single.Edit;
    Form1.SingleGrid.SelectedField.Value := 'True';
    DM1.Single.Next;
    Form1.SingleGrid.SelectedIndex := 1;
  end;
  Form1.SetFocus;
end;

procedure TS_APQSDlg.FalseButtonClick(Sender: TObject);
begin
  if Form1.SingleGrid.SelectedIndex = 6 then
  begin
    DM1.Single.Edit;
    Form1.SingleGrid.SelectedField.Value := 'False';
    DM1.Single.Next;
    Form1.SingleGrid.SelectedIndex := 1;
  end;
  Form1.SetFocus;
end;

end.
