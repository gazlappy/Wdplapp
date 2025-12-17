unit DRatings;

interface

uses Windows, SysUtils, Messages, Classes, Graphics, Controls,
  StdCtrls, ExtCtrls, Forms, Quickrpt, QRCtrls, Db, DBTables;

type
  TDoublesReport = class(TQuickRep)
    ColumnHeaderBand1: TQRBand;
    DetailBand1: TQRBand;
    QRLabel4: TQRLabel;
    QRLabel3: TQRLabel;
    QRLabel5: TQRLabel;
    QRLabel8: TQRLabel;
    QRLabel6: TQRLabel;
    QRLabel7: TQRLabel;
    QRLabel9: TQRLabel;
    QRLabel11: TQRLabel;
    QRLabel10: TQRLabel;
    QRSysData2: TQRSysData;
    QRDBText3: TQRDBText;
    QRDBText2: TQRDBText;
    QRDBText4: TQRDBText;
    QRDBText6: TQRDBText;
    QRDBText5: TQRDBText;
    QRDBText7: TQRDBText;
    QRDBText8: TQRDBText;
    QRDBText9: TQRDBText;
    QRDBText11: TQRDBText;
    QRDBText12: TQRDBText;
    QRDBText13: TQRDBText;
    TitleBand1: TQRBand;
    QRShape1: TQRShape;
    QRLabel14: TQRLabel;
    QRDBText10: TQRDBText;
    QRLabel2: TQRLabel;
    QRDBText1: TQRDBText;
    QRLabel16: TQRLabel;
    QRLabel12: TQRLabel;
    QRLabel1: TQRLabel;
    QRShape2: TQRShape;
    QRLabel15: TQRLabel;
    QRLabel17: TQRLabel;
    QRSysData3: TQRSysData;
    QRShape3: TQRShape;
    QRDBText14: TQRDBText;
    DivisionLabel: TQRLabel;
    QRLabel13: TQRLabel;
    procedure DetailBand1AfterPrint(Sender: TQRCustomBand;
      BandPrinted: Boolean);
    procedure QuickRepBeforePrint(Sender: TCustomQuickRep;
      var PrintReport: Boolean);
  private

  public

  end;

var
  DoublesReport: TDoublesReport;
  NoPrinted: Integer;

implementation

uses datamodule;

{$R *.DFM}

procedure TDoublesReport.DetailBand1AfterPrint(Sender: TQRCustomBand;
  BandPrinted: Boolean);
begin
NoPrinted := NoPrinted + 1;
if NoPrinted >= DM1.LeagueShowTop.Value then
  DM1.PairQuery.Last;
end;

procedure TDoublesReport.QuickRepBeforePrint(Sender: TCustomQuickRep;
  var PrintReport: Boolean);
begin
  NoPrinted := 0;
  DM1.PairQuery.Close;
  DM1.PairQuery.Open;
  DM1.PairQuery.First;
  DM1.Team.FindKey([DM1.PairQueryPlayerTeam.Value]);
  DM1.Division.FindKey([DM1.TeamDivision.Value]);
  DivisionLabel.Caption := DM1.LeagueSeason.Value + ' ' + DM1.DivisionFullDivisionName.Value;
end;

end.
