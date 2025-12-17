unit resrpt;

interface

uses Windows, SysUtils, Messages, Classes, Graphics, Controls,
  StdCtrls, ExtCtrls, Forms, Quickrpt, QRCtrls, Db, DBTables;

type
  TResultReport = class(TQuickRep)
    ColumnHeaderBand1: TQRBand;
    DetailBand1: TQRBand;
    QRDBText1: TQRDBText;
    QRDBText2: TQRDBText;
    QRLabel1: TQRLabel;
    QRLabel2: TQRLabel;
    QRLabel3: TQRLabel;
    QRLabel4: TQRLabel;
    QRLabel5: TQRLabel;
    QRLabel6: TQRLabel;
    QRLabel7: TQRLabel;
    QRLabel8: TQRLabel;
    QRLabel9: TQRLabel;
    QRLabel10: TQRLabel;
    QRLabel11: TQRLabel;
    QRLabel12: TQRLabel;
    QRLabel13: TQRLabel;
    QRLabel14: TQRLabel;
    QRLabel15: TQRLabel;
    QRLabel16: TQRLabel;
    QRLabel19: TQRLabel;
    QRLabel20: TQRLabel;
    QRLabel21: TQRLabel;
    QRLabel22: TQRLabel;
    TitleBand1: TQRBand;
    QRShape1: TQRShape;
    QRLabel18: TQRLabel;
    QRDBText3: TQRDBText;
    QRLabel23: TQRLabel;
    QRLabel25: TQRLabel;
    QRShape2: TQRShape;
    QRLabel26: TQRLabel;
    QRLabel27: TQRLabel;
    QRSysData3: TQRSysData;
    QRShape3: TQRShape;
    QRDBText14: TQRDBText;
    QRLabel28: TQRLabel;
    procedure QuickRepStartPage(Sender: TCustomQuickRep);
    procedure DetailBand1BeforePrint(Sender: TQRCustomBand;
      var PrintBand: Boolean);
    procedure DetailBand1AfterPrint(Sender: TQRCustomBand;
      BandPrinted: Boolean);
    procedure QuickRepBeforePrint(Sender: TCustomQuickRep;
      var PrintReport: Boolean);
  private

  public
    holddate: String;
    holddiv: String;
  end;

var
  ResultReport: TResultReport;

implementation

uses datamodule;

{$R *.DFM}

procedure TResultReport.QuickRepStartPage(Sender: TCustomQuickRep);
begin
  holddate := '';
  holddiv := '';
end;

procedure TResultReport.DetailBand1BeforePrint(Sender: TQRCustomBand;
  var PrintBand: Boolean);
var hometotal, awaytotal: Double;
begin
  QRLabel1.Caption := FloatToStr(DM1.MatchQueryHSWins.Value);
  QRLabel2.Caption := FloatToStr(DM1.MatchQueryHDWins.Value);
  hometotal := DM1.MatchQueryHSWins.Value * DM1.LeagueSinglesBonus.Value;
  hometotal := hometotal + (DM1.MatchQueryHDWins.Value * DM1.LeagueDoublesBonus.Value);
  QRLabel4.Caption := FloatToStr(DM1.MatchQueryASWins.Value);
  QRLabel5.Caption := FloatToStr(DM1.MatchQueryADWins.Value);
  awaytotal := DM1.MatchQueryASWins.Value * DM1.LeagueSinglesBonus.Value;
  awaytotal := awaytotal + (DM1.MatchQueryADWins.Value * DM1.LeagueDoublesBonus.Value);
  if hometotal > awaytotal then
  begin
    hometotal := hometotal + DM1.LeagueWinBonus.Value;
    awaytotal := awaytotal + DM1.LeagueLossBonus.Value;
    QRLabel7.Caption := 'beat'
    end;
  if hometotal = awaytotal then
  begin
    hometotal := hometotal + DM1.LeagueDrawBonus.Value;
    awaytotal := awaytotal + DM1.LeagueDrawBonus.Value;
    QRLabel7.Caption := 'drew with'
  end;
  if hometotal < awaytotal then
  begin
    hometotal := hometotal + DM1.LeagueLossBonus.Value;
    awaytotal := awaytotal + DM1.LeagueWinBonus.Value;
    QRLabel7.Caption := 'lost to'
  end;
  QRLabel3.Caption := FloatToStr(hometotal);
  QRLabel6.Caption := FloatToStr(awaytotal);
  if DateToStr(DM1.MatchQueryMatchDate.Value) <> holddate then
  begin
    holddate := DateToStr(DM1.MatchQueryMatchDate.Value);
    QRLabel21.Caption := DateToStr(DM1.MatchQueryMatchDate.Value);
    holddiv := '';
  end;
  if DM1.MatchQueryDivName.Value <> holddiv then
  begin
    holddiv := DM1.MatchQueryDivName.Value;
    QRLabel22.Caption := DM1.MatchQueryDivName.Value;
  end;
end;

procedure TResultReport.DetailBand1AfterPrint(Sender: TQRCustomBand;
  BandPrinted: Boolean);
begin
  QRLabel21.Caption := '';
  QRLabel22.Caption := '';
end;

procedure TResultReport.QuickRepBeforePrint(Sender: TCustomQuickRep;
  var PrintReport: Boolean);
begin
  DM1.MatchQuery.First;
end;

end.
