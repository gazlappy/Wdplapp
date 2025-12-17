unit S_HPQS;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls,
  Buttons, ExtCtrls, Grids, DBGrids, Menus;

type
  TS_HPQSDlg = class(TForm)
    DBGrid1: TDBGrid;
    HomeButton: TBitBtn;
    AwayButton: TBitBtn;
    TrueButton: TBitBtn;
    FalseButton: TBitBtn;
    procedure DBGrid1DblClick(Sender: TObject);
    procedure TrueButtonClick(Sender: TObject);
    procedure FalseButtonClick(Sender: TObject);
    procedure HomeButtonClick(Sender: TObject);
    procedure AwayButtonClick(Sender: TObject);
    procedure Close1Click(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  S_HPQSDlg: TS_HPQSDlg;

implementation

uses datamodule, Main;

{$R *.DFM}

procedure TS_HPQSDlg.DBGrid1DblClick(Sender: TObject);
begin
  if Form1.SingleGrid.SelectedIndex = 1 then
  begin
    DM1.Single.Edit;
    Form1.SingleGrid.SelectedIndex := Form1.SingleGrid.SelectedIndex + 1;
    Form1.SingleGrid.SelectedField.Value := DM1.HomePlayerLookUpPlayerNo.Value;
  end;
  Form1.SetFocus;
end;

procedure TS_HPQSDlg.TrueButtonClick(Sender: TObject);
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

procedure TS_HPQSDlg.FalseButtonClick(Sender: TObject);
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

procedure TS_HPQSDlg.HomeButtonClick(Sender: TObject);
begin
  if Form1.SingleGrid.SelectedIndex = 5 then
  begin
    DM1.Single.Edit;
    Form1.SingleGrid.SelectedField.Value := 'Home';
    Form1.SingleGrid.SelectedIndex := Form1.SingleGrid.SelectedIndex + 1;
  end;
  Form1.SetFocus;
end;

procedure TS_HPQSDlg.AwayButtonClick(Sender: TObject);
begin
  if Form1.SingleGrid.SelectedIndex = 5 then
  begin
    DM1.Single.Edit;
    Form1.SingleGrid.SelectedField.Value := 'Away';
    Form1.SingleGrid.SelectedIndex := Form1.SingleGrid.SelectedIndex + 1;
  end;
  Form1.SetFocus;
end;

procedure TS_HPQSDlg.Close1Click(Sender: TObject);
begin
  S_HPQSDlg.Close;
end;

end.
