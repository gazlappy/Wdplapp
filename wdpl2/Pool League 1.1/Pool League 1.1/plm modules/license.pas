unit license;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  Registry, StdCtrls;

type
  TLicForm = class(TForm)
    Label1: TLabel;
    Label2: TLabel;
    procedure Enter(var SerialNo: String; var DaysLeft: Double);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  LicForm: TLicForm;

implementation

uses Main;

{$R *.DFM}

procedure TLicForm.Enter(var SerialNo: String; var DaysLeft: Double);
var Reggie: TRegistry;
var MyKey, LicDate, LockCode, KeyCode: String;
var xserial, xlock, xkey, xldate: String;
var a1,b1,c1,d1,e1,f1,g1,h1,i1,j1,k1,l1,m1,n1,o1,p1,q1,r1: String;
var neednewkey, firsttime: Boolean;
var dummy, extra, outtext: String;
begin
  firsttime := False;
  neednewkey := False;
  MyKey := '\Software\Systems4Sport\' + Application.Title;
  Reggie := TRegistry.Create;
  try
    Reggie.RootKey := HKEY_LOCAL_MACHINE;
    Reggie.OpenKey(MyKey,True);
    SerialNo := Reggie.ReadString('SerialNo');
    LockCode := Reggie.ReadString('LockCode');
    LicDate := Reggie.ReadString('LicDate');
    KeyCode := Reggie.ReadString('KeyCode');
  except
    ShowMessage('Error');
    ShowMessage('Unable to locate license information.  This application will terminate!');
    Application.Terminate;
  end;
  if SerialNo = 'FirstTimeStartUp' then
  begin
    xserial := TimeToStr(Time);
    SerialNo := Copy(xserial,4,2) + Copy(xserial,7,2);
    xserial := DateToStr(Date);
    SerialNo := SerialNo + Copy(xserial,1,2);
    xserial := '';
    ShowMessage('You are using this product for the first time. Follow the instructions in the next window.');
    firsttime := true;
  end;
  if LicDate = '' then
    DaysLeft := 0
  else
    DaysLeft := StrToDate(LicDate) - Date;
  if firsttime then DaysLeft := 99; // Force thru
  if LockCode <> '' then
    neednewkey := true
  else
  begin
    if DaysLeft < 29 then
    begin
      neednewkey := true;
      dummy := TimeToStr(Time);
      LockCode := Copy(dummy,1,2) + Copy(dummy,4,2) + Copy(dummy,7,2);
    end;
  end;
  if neednewkey then
  begin
    if DaysLeft < 1 then
      outtext := 'Your license has expired'
    else
      outtext := 'Your license expires in ' + FloatToStr(DaysLeft) + ' days';
    outtext := outtext + '.  ';
    outtext := outtext + 'You need to license in order to continue using this product after the evaluation period.  Systems4Sport offer a free 28 day evaluation period and you may qualify for three months free.';
    outtext := outtext + '  ';
    outtext := outtext + 'To recieve your license, email license@systems4sport.co.uk with your name, address and both the Lock Code (';
    outtext := outtext + LockCode + ') and Serial Number (' + SerialNo + ').';
    outtext := outtext + '  Systems4Sport will return a license key.  This should be entered in the next window.  If your license has not yet expired or you do not have a key, click ''Cancel'' instead.';
    MessageDlg(outtext, mtWarning, [mbOK], 0);
    InputQuery('License','Enter your 18 digit license key below or click cancel',xkey);
    j1 := Copy(xkey,1,1);
    l1 := Copy(xkey,2,1);
    g1 := Copy(xkey,3,1);
    q1 := Copy(xkey,4,1);
    b1 := Copy(xkey,5,1);
    f1 := Copy(xkey,6,1);
    c1 := Copy(xkey,7,1);
    p1 := Copy(xkey,8,1);
    d1 := Copy(xkey,9,1);
    n1 := Copy(xkey,10,1);
    m1 := Copy(xkey,11,1);
    e1 := Copy(xkey,12,1);
    o1 := Copy(xkey,13,1);
    a1 := Copy(xkey,14,1);
    k1 := Copy(xkey,15,1);
    h1 := Copy(xkey,16,1);
    i1 := Copy(xkey,17,1);
    r1 := Copy(xkey,18,1);
    xserial := m1 + n1 + o1 + p1 + q1 + r1;
    xlock := g1 + h1 + i1 + j1 + k1 + l1;
    if (xserial = SerialNo) and (xlock = LockCode) then
    begin
      LockCode := '';
      SerialNo := xserial;
      KeyCode := xkey;
      dummy := a1 + b1 + '/' + c1 + d1 + '/' + e1 + f1;
      LicDate := dummy;
      ShowMessage('License extended until ' + dummy);
    end
    else
    begin
      ShowMessage('Licencing failed!');
    end;
  end;
  if firsttime then
  begin
    dummy := TimeToStr(Time);
    LockCode := Copy(dummy,1,2) + Copy(dummy,4,2) + Copy(dummy,7,2);
    LicDate := DateToStr(Date + 28);
    KeyCode := Copy(LicDate,1,2) + Copy(LicDate,4,2) + Copy(LicDate,7,2) + LockCode + SerialNo;
    a1 := Copy(KeyCode,1,1);
    b1 := Copy(KeyCode,2,1);
    c1 := Copy(KeyCode,3,1);
    d1 := Copy(KeyCode,4,1);
    e1 := Copy(KeyCode,5,1);
    f1 := Copy(KeyCode,6,1);
    g1 := Copy(KeyCode,7,1);
    h1 := Copy(KeyCode,8,1);
    i1 := Copy(KeyCode,9,1);
    j1 := Copy(KeyCode,10,1);
    k1 := Copy(KeyCode,11,1);
    l1 := Copy(KeyCode,12,1);
    m1 := Copy(KeyCode,13,1);
    n1 := Copy(KeyCode,14,1);
    o1 := Copy(KeyCode,15,1);
    p1 := Copy(KeyCode,16,1);
    q1 := Copy(KeyCode,17,1);
    r1 := Copy(KeyCode,18,1);
    KeyCode := j1+l1+g1+q1+b1+f1+c1+p1+d1+n1+m1+e1+o1+a1+k1+h1+i1+r1;
  end;
  if LicDate = '' then
    DaysLeft := 0
  else
    DaysLeft := StrToDate(LicDate) - Date;
// Validate key
  j1 := Copy(KeyCode,1,1);
  l1 := Copy(KeyCode,2,1);
  g1 := Copy(KeyCode,3,1);
  q1 := Copy(KeyCode,4,1);
  b1 := Copy(KeyCode,5,1);
  f1 := Copy(KeyCode,6,1);
  c1 := Copy(KeyCode,7,1);
  p1 := Copy(KeyCode,8,1);
  d1 := Copy(KeyCode,9,1);
  n1 := Copy(KeyCode,10,1);
  m1 := Copy(KeyCode,11,1);
  e1 := Copy(KeyCode,12,1);
  o1 := Copy(KeyCode,13,1);
  a1 := Copy(KeyCode,14,1);
  k1 := Copy(KeyCode,15,1);
  h1 := Copy(KeyCode,16,1);
  i1 := Copy(KeyCode,17,1);
  r1 := Copy(KeyCode,18,1);
  extra := Copy(KeyCode,19,1);
  xserial := m1 + n1 + o1 + p1 + q1 + r1;
  xldate := a1 + b1 + '/' + c1 + d1 + '/' + e1 + f1;
  if DaysLeft < 1 then
  begin
    ShowMessage('License expired.  This application will terminate!');
    Application.Terminate;
  end;
  if (xserial <> SerialNo) or (xldate <> LicDate) then
  begin
    ShowMessage('An attempt to alter license information has been detected.  This application will terminate!');
    ShowMessage('In order to re-license this product, re-launch the application and follow the instructions.');
    dummy := TimeToStr(Time);
    LockCode := Copy(dummy,1,2) + Copy(dummy,4,2) + Copy(dummy,7,2);
    Reggie.WriteString('LockCode',LockCode);
    Application.Terminate;
  end;
  Reggie.WriteString('SerialNo',SerialNo);
  Reggie.WriteString('LicDate',LicDate);
  Reggie.WriteString('KeyCode',KeyCode);
  Reggie.CloseKey;
  Reggie.Free;
end;

end.
