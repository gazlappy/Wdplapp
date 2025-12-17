unit dtdialog;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  Buttons, StdCtrls;

type
  TSelectDt = class(TForm)
    PrintButton: TButton;
    PreviewButton: TButton;
    CancelButton: TBitBtn;
    Edit1: TEdit;
    SpeedButton1: TSpeedButton;
    procedure SpeedButton1Click(Sender: TObject);
    procedure PreviewButtonClick(Sender: TObject);
  private
    { Private declarations }
  public
    Preview: Boolean;
    { Public declarations }
  end;

var
  SelectDt: TSelectDt;

implementation

uses PickDate;

{$R *.DFM}

procedure TSelectDt.SpeedButton1Click(Sender: TObject);
begin
  PickDt.Date := Now;
  if PickDt.ShowModal = mrOK then
    Edit1.Text := DateToStr(PickDt.Date);
end;

procedure TSelectDt.PreviewButtonClick(Sender: TObject);
begin
  Preview := True;
end;

end.
