unit evaluate;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  Db, StdCtrls, DBCtrls, Buttons;

type
  TForm2 = class(TForm)
    Memo1: TMemo;
    Label1: TLabel;
    DBText1: TDBText;
    Label2: TLabel;
    DBText2: TDBText;
    LS: TDataSource;
    OKBtn: TBitBtn;
    procedure Button1Click(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  Form2: TForm2;

implementation

uses datamodule, main;
{$R *.DFM}

procedure TForm2.Button1Click(Sender: TObject);
begin
  ModalResult := mrOK;
end;

end.
