unit PRating;

interface

uses Windows, SysUtils, Messages, Classes, Graphics, Controls,
  StdCtrls, ExtCtrls, Forms, Quickrpt, QRCtrls, Db, DBTables, Dialogs;

type
  TRatingReport = class(TQuickRep)
    ColumnHeaderBand1: TQRBand;
    DetailBand1: TQRBand;
    QRLabel3: TQRLabel;
    QRDBText2: TQRDBText;
    QRLabel4: TQRLabel;
    QRSysData2: TQRSysData;
    QRDBText3: TQRDBText;
    QRLabel5: TQRLabel;
    QRDBText4: TQRDBText;
    QRLabel6: TQRLabel;
    QRLabel7: TQRLabel;
    QRDBText5: TQRDBText;
    QRLabel8: TQRLabel;
    QRDBText6: TQRDBText;
    QRDBText7: TQRDBText;
    QRLabel9: TQRLabel;
    QRDBText8: TQRDBText;
    QRLabel11: TQRLabel;
    QRDBText9: TQRDBText;
    QRLabel10: TQRLabel;
    QRDBText11: TQRDBText;
    QRLabel1: TQRLabel;
    QRDBText10: TQRDBText;
    TitleBand1: TQRBand;
    QRShape1: TQRShape;
    QRLabel14: TQRLabel;
    QRDBText1: TQRDBText;
    QRLabel2: TQRLabel;
    QRDBText12: TQRDBText;
    QRLabel16: TQRLabel;
    QRLabel12: TQRLabel;
    QRLabel13: TQRLabel;
    QRShape2: TQRShape;
    QRLabel15: TQRLabel;
    QRLabel17: TQRLabel;
    QRSysData3: TQRSysData;
    QRShape3: TQRShape;
    QRDBText14: TQRDBText;
    DivisionLabel: TQRLabel;
    QRLabel18: TQRLabel;
    procedure DetailBand1AfterPrint(Sender: TQRCustomBand;
      BandPrinted: Boolean);
    procedure QuickRepBeforePrint(Sender: TCustomQuickRep;
      var PrintReport: Boolean);
  private
  public

  end;

var
  RatingReport: TRatingReport;
  NoPrinted: Integer;
implementation

uses datamodule;

{$R *.DFM}

procedure TRatingReport.DetailBand1AfterPrint(Sender: TQRCustomBand;
  BandPrinted: Boolean);
begin
NoPrinted := NoPrinted + 1;
if NoPrinted >= DM1.LeagueShowTop.Value then
  DM1.PlayerQuery.Last;
end;

procedure TRatingReport.QuickRepBeforePrint(Sender: TCustomQuickRep;
  var PrintReport: Boolean);
begin
  NoPrinted := 0;
  DM1.PlayerQuery.Close;
  DM1.PlayerQuery.Open;
  DM1.PlayerQuery.First;
  DM1.Team.Open;
  DM1.Team.FindKey([DM1.PlayerQueryPlayerTeam.Value]);
  DM1.Division.Open;
  DM1.Division.FindKey([DM1.TeamDivision.Value]);
  DivisionLabel.Caption := DM1.LeagueSeason.Value + ' ' + DM1.DivisionFullDivisionName.Value;
end;

end.
