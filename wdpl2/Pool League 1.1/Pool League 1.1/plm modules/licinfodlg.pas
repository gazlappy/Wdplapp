unit licinfodlg;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls,
  Buttons, ExtCtrls;

type
  TLicInfo = class(TForm)
    OKBtn: TButton;
    Memo1: TMemo;
    Label1: TLabel;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  LicInfo: TLicInfo;

implementation

{$R *.DFM}

end.
