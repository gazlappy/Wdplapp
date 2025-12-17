unit D_HPQS;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls,
  Buttons, ExtCtrls, Grids, DBGrids;

type
  TD_HPQSDlg = class(TForm)
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
  D_HPQSDlg: TD_HPQSDlg;

implementation

uses Main, datamodule;

{$R *.DFM}

procedure TD_HPQSDlg.DBGrid1DblClick(Sender: TObject);
begin
  if (Form1.DoubleGrid.SelectedIndex = 1) or (Form1.DoubleGrid.SelectedIndex = 3) then
  begin
    DM1.Double.Edit;
    Form1.DoubleGrid.SelectedIndex := Form1.DoubleGrid.SelectedIndex + 1;
    Form1.DoubleGrid.SelectedField.Value := DM1.HomePlayerLookUpPlayerNo.Value;
  end;
  Form1.SetFocus;
end;

procedure TD_HPQSDlg.HomeButtonClick(Sender: TObject);
begin
  if Form1.DoubleGrid.SelectedIndex = 9 then
  begin
    DM1.Double.Edit;
    Form1.DoubleGrid.SelectedField.Value := 'Home';
    Form1.DoubleGrid.SelectedIndex := Form1.DoubleGrid.SelectedIndex + 1;
  end;
  Form1.SetFocus;
end;

procedure TD_HPQSDlg.AwayButtonClick(Sender: TObject);
begin
  if Form1.DoubleGrid.SelectedIndex = 9 then
  begin
    DM1.Double.Edit;
    Form1.DoubleGrid.SelectedField.Value := 'Away';
    Form1.DoubleGrid.SelectedIndex := Form1.DoubleGrid.SelectedIndex + 1;
  end;
  Form1.SetFocus;
end;

procedure TD_HPQSDlg.TrueButtonClick(Sender: TObject);
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

procedure TD_HPQSDlg.FalseButtonClick(Sender: TObject);
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
