unit pickdate;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  Buttons, StdCtrls;

type
  TSelectDt = class(TForm)
    Edit1: TEdit;
    PrintButton: TButton;
    PreviewButton: TButton;
    CancelButton: TBitBtn;
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

{$R *.DFM}

procedure TSelectDt.PreviewButtonClick(Sender: TObject);
begin
Preview := True;
end;

end.
