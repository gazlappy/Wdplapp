unit evaluate2;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls, 
  Buttons, ExtCtrls, Db, DBCtrls;

type
  TLicDialog = class(TForm)
    Memo1: TMemo;
    Label1: TLabel;
    DBText1: TDBText;
    Label2: TLabel;
    DBText2: TDBText;
    OKBtn: TBitBtn;
    LS: TDataSource;
    procedure OKBtnClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  LicDialog: TLicDialog;

implementation

uses main;
{$R *.DFM}


procedure TLicDialog.OKBtnClick(Sender: TObject);
begin
  ModalResult := mrOK;
end;

end.
