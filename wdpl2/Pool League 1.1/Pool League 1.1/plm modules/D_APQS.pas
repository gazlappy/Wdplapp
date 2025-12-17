unit D_APQS;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls,
  Buttons, ExtCtrls, Grids, DBGrids;

type
  TD_APQSDlg = class(TForm)
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
  D_APQSDlg: TD_APQSDlg;

implementation

uses Main, datamodule;

{$R *.DFM}

procedure TD_APQSDlg.DBGrid1DblClick(Sender: TObject);
begin
  if (Form1.DoubleGrid.SelectedIndex = 5) or (Form1.DoubleGrid.SelectedIndex = 7) then
  begin
    DM1.Double.Edit;
    Form1.DoubleGrid.SelectedIndex := Form1.DoubleGrid.SelectedIndex + 1;
    Form1.DoubleGrid.SelectedField.Value := DM1.AwayPlayerLookUpPlayerNo.Value;
  end;
  Form1.SetFocus;

end;

procedure TD_APQSDlg.HomeButtonClick(Sender: TObject);
begin
  if Form1.DoubleGrid.SelectedIndex = 9 then
  begin
    DM1.Double.Edit;
    Form1.DoubleGrid.SelectedField.Value := 'Home';
    Form1.DoubleGrid.SelectedIndex := Form1.DoubleGrid.SelectedIndex + 1;
  end;
  Form1.SetFocus;
end;

procedure TD_APQSDlg.AwayButtonClick(Sender: TObject);
begin
  if Form1.DoubleGrid.SelectedIndex = 9 then
  begin
    DM1.Double.Edit;
    Form1.DoubleGrid.SelectedField.Value := 'Away';
    Form1.DoubleGrid.SelectedIndex := Form1.DoubleGrid.SelectedIndex + 1;
  end;
  Form1.SetFocus;
end;

procedure TD_APQSDlg.TrueButtonClick(Sender: TObject);
begin
  if (Form1.DoubleGrid.SelectedIndex = 10) or (Form1.DoubleGrid.SelectedIndex = 11) then
  begin
    DM1.Double.Edit;
    Form1.DoubleGrid.SelectedField.Value := 'True';
    if Form1.DoubleGrid.SelectedIndex = 11 then
    begin
      DM1.Double.Next;
      Form1.DoubleGrid.SelectedIndex := 1;
    end;
    if Form1.DoubleGrid.SelectedIndex = 10 then
      Form1.DoubleGrid.SelectedIndex := 11;
  end;
  Form1.SetFocus;
end;

procedure TD_APQSDlg.FalseButtonClick(Sender: TObject);
begin
  if (Form1.DoubleGrid.SelectedIndex = 10) or (Form1.DoubleGrid.SelectedIndex = 11) then
  begin
    DM1.Double.Edit;
    Form1.DoubleGrid.SelectedField.Value := 'False';
    if Form1.DoubleGrid.SelectedIndex = 11 then
    begin
      DM1.Double.Next;
      Form1.DoubleGrid.SelectedIndex := 1;
    end;
    if Form1.DoubleGrid.SelectedIndex = 10 then
      Form1.DoubleGrid.SelectedIndex := 11;
  end;
  Form1.SetFocus;
end;

end.
