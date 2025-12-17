unit selectdt;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, Buttons;

type
  TPickDate = class(TForm)
    PrintButton: TButton;
    PreviewButton: TButton;
    CancelButton: TBitBtn;
    Edit1: TEdit;
    SpeedButton1: TSpeedButton;
    procedure SpeedButton1Click(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  PickDate: TPickDate;

implementation

{$R *.DFM}

procedure TPickDate.SpeedButton1Click(Sender: TObject);
begin
if Calendar.ShowModal then
Edit1.Text :=
end;

end.
