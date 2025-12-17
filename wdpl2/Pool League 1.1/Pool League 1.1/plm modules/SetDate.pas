unit SetDate;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, Buttons, ComCtrls, Mask;

type
  TSelectDt = class(TForm)
    PrintButton: TButton;
    PreviewButton: TButton;
    CancelButton: TBitBtn;
    MaskEdit1: TMaskEdit;
    SpeedButton1: TSpeedButton;
    procedure PreviewButtonClick(Sender: TObject);
    procedure PrintButtonClick(Sender: TObject);
    procedure CancelButtonClick(Sender: TObject);
    procedure FormShow(Sender: TObject);
    procedure SpeedButton1Click(Sender: TObject);
    procedure MaskEdit1Exit(Sender: TObject);
  private
    { Private declarations }
  public
    Preview: Boolean;
    { Public declarations }
  end;

var
  SelectDt: TSelectDt;

implementation

uses resrpt, Main, datamodule, pickdt;

{$R *.DFM}

procedure TSelectDt.PreviewButtonClick(Sender: TObject);
begin
  ResultReport.Preview;
end;

procedure TSelectDt.PrintButtonClick(Sender: TObject);
begin
  ResultReport.Print;
end;

procedure TSelectDt.CancelButtonClick(Sender: TObject);
begin
  ModalResult := mrOK;
end;

procedure TSelectDt.FormShow(Sender: TObject);
begin
  MaskEdit1.Text := DateToStr(Date - 28);
end;

procedure TSelectDt.SpeedButton1Click(Sender: TObject);
begin
  if PickDate.ShowModal = mrOK then
    MaskEdit1.Text := DateToStr(PickDate.Calendar1.CalendarDate);
end;

procedure TSelectDt.MaskEdit1Exit(Sender: TObject);
var dummy: TDate;
begin
  DM1.MatchQuery.Close;
  dummy := StrToDate(MaskEdit1.Text);
  DM1.MatchQuery.Params.ParamByName('SelectedDate').AsDate := dummy;
  DM1.MatchQuery.Open;
end;

end.
