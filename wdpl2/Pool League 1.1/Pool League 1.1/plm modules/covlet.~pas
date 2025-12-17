unit covlet;

interface

uses Windows, SysUtils, Messages, Classes, Graphics, Controls,
  StdCtrls, ExtCtrls, Forms, Quickrpt, QRCtrls, Db, DBTables;

type
  TCovletVenue = class(TQuickRep)
    PageHeaderBand1: TQRBand;
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
    QRDBText2: TQRDBText;
    QRLabel8: TQRLabel;
    QRLabel9: TQRLabel;
    procedure QuickRepBeforePrint(Sender: TCustomQuickRep;
      var PrintReport: Boolean);
    procedure DetailBand1BeforePrint(Sender: TQRCustomBand;
      var PrintBand: Boolean);
  private

  public

  end;

var
  CovletVenue: TCovletVenue;

implementation

uses Main, plmmailshot, datamodule;

{$R *.DFM}

procedure TCovletVenue.QuickRepBeforePrint(Sender: TCustomQuickRep;
  var PrintReport: Boolean);
begin
  DM1.League.First;
end;

procedure TCovletVenue.DetailBand1BeforePrint(Sender: TQRCustomBand;
  var PrintBand: Boolean);
begin
  QRLabel1.Caption := Mailshot.VenueQueryVenue.Value;
  QRLabel2.Caption := Mailshot.VenueQueryAddressLine1.Value;
  QRLabel3.Caption := Mailshot.VenueQueryAddressLine2.Value;
  QRLabel4.Caption := Mailshot.VenueQueryAddressLine3.Value;
  QRLabel5.Caption := Mailshot.VenueQueryAddressLine4.Value;
  QRLabel7.Caption := 'Re: ' + DM1.LeagueLeagueName.Value;
  QRLabel9.Caption := DM1.LeagueAdministrator.Value;
end;

end.
