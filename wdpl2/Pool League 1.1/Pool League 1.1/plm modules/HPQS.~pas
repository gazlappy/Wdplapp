unit HPQS;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls,
  Buttons, ExtCtrls, Grids, DBGrids;

type
  THPQSDlg = class(TForm)
    DBGrid1: TDBGrid;
    procedure DBGrid1DblClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  HPQSDlg: THPQSDlg;

implementation

uses datamodule, Main;

{$R *.DFM}

procedure THPQSDlg.DBGrid1DblClick(Sender: TObject);
begin
  if Form1.SingleGrid.SelectedIndex = 1 then
  begin
    DM1.Single.Edit;
    Form1.SingleGrid.SelectedIndex := Form1.SingleGrid.SelectedIndex + 1;
    Form1.SingleGrid.SelectedField.Value := DM1.HomePlayerLookUpPlayerNo.Value;
  end;
  Form1.SetFocus;
end;

end.
