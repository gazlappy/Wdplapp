unit covlet2;

interface

uses Windows, SysUtils, Messages, Classes, Graphics, Controls,
  StdCtrls, ExtCtrls, Forms, Quickrpt, QRCtrls, Db, DBTables;

type
  TCovletCaptain = class(TQuickRep)
    QRBand1: TQRBand;
    DetailBand1: TQRBand;
    QRDBText1: TQRDBText;
    QRLabel1: TQRLabel;
    QRLabel2: TQRLabel;
    QRLabel3: TQRLabel;
    QRLabel4: TQRLabel;
    QRLabel5: TQRLabel;
    QRSysData1: TQRSysData;
    QRLabel6: TQRLabel;
    QRLabel7: TQRLabel;
    QRLabel8: TQRLabel;
    QRLabel9: TQRLabel;
    QRDBText2: TQRDBText;
    procedure QRBand1BeforePrint(Sender: TQRCustomBand;
      var PrintBand: Boolean);
    procedure QuickRepBeforePrint(Sender: TCustomQuickRep;
      var PrintReport: Boolean);
  private

  public

  end;

var
  CovletCaptain: TCovletCaptain;

implementation

uses Main, plmmailshot, datamodule;

{$R *.DFM}

procedure TCovletCaptain.QRBand1BeforePrint(Sender: TQRCustomBand;
  var PrintBand: Boolean);
begin
  QRLabel1.Caption := Mailshot.TeamQuery2Contact.Value;
  QRLabel2.Caption := Mailshot.TeamQuery2ContactAddress1.Value;
  QRLabel3.Caption := Mailshot.TeamQuery2ContactAddress2.Value;
  QRLabel4.Caption := Mailshot.TeamQuery2ContactAddress3.Value;
  QRLabel5.Caption := Mailshot.TeamQuery2ContactAddress4.Value;
  QRLabel7.Caption := 'Re: ' + DM1.LeagueLeagueName.Value;
  QRLabel9.Caption := DM1.LeagueAdministrator.Value;
end;

procedure TCovletCaptain.QuickRepBeforePrint(Sender: TCustomQuickRep;
  var PrintReport: Boolean);
begin
  DM1.League.First;
end;

end.
