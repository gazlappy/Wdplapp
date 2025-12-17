unit plreport;

interface

uses Windows, SysUtils, Messages, Classes, Graphics, Controls,
  StdCtrls, ExtCtrls, Forms, Quickrpt, QRCtrls, DBTables, Db, Dialogs;

type
  TPlayerReport = class(TQuickRep)
    ColumnHeaderBand1: TQRBand;
    DetailBand1: TQRBand;
    PageFooterBand1: TQRBand;
    SummaryBand1: TQRBand;
    QRLabel1: TQRLabel;
    QRLabel3: TQRLabel;
    QRLabel4: TQRLabel;
    QRLabel5: TQRLabel;
    QRDBText2: TQRDBText;
    QRLabel6: TQRLabel;
    QRLabel7: TQRLabel;
    QRDBText3: TQRDBText;
    QRLabel9: TQRLabel;
    QRLabel10: TQRLabel;
    QRLabel11: TQRLabel;
    QRLabel12: TQRLabel;
    QRLabel13: TQRLabel;
    QRLabel14: TQRLabel;
    QRDBText5: TQRDBText;
    QRShape1: TQRShape;
    QRLabel15: TQRLabel;
    QRLabel16: TQRLabel;
    QRLabel17: TQRLabel;
    QRLabel18: TQRLabel;
    QRLabel19: TQRLabel;
    QRLabel20: TQRLabel;
    QRLabel21: TQRLabel;
    QRDBText6: TQRDBText;
    QRDBText7: TQRDBText;
    QRDBText8: TQRDBText;
    QRDBText9: TQRDBText;
    QRDBText10: TQRDBText;
    QRDBText11: TQRDBText;
    QRLabel22: TQRLabel;
    QRDBText12: TQRDBText;
    QRLabel23: TQRLabel;
    QRMemo1: TQRMemo;
    TitleBand1: TQRBand;
    QRShape2: TQRShape;
    QRLabel26: TQRLabel;
    QRShape3: TQRShape;
    QRLabel27: TQRLabel;
    QRLabel28: TQRLabel;
    QRSysData3: TQRSysData;
    QRShape4: TQRShape;
    QRDBText14: TQRDBText;
    DivisionLabel: TQRLabel;
    QRLabel29: TQRLabel;
    procedure TitleBand1BeforePrint(Sender: TQRCustomBand;
      var PrintBand: Boolean);
    procedure DetailBand1BeforePrint(Sender: TQRCustomBand;
      var PrintBand: Boolean);
    procedure SummaryBand1BeforePrint(Sender: TQRCustomBand;
      var PrintBand: Boolean);
  private

  public

  end;

var
  PlayerReport: TPlayerReport;
  Weighting, TotalWeight, TotalValue: Integer;

implementation

uses datamodule, Player;

{$R *.DFM}

procedure TPlayerReport.TitleBand1BeforePrint(Sender: TQRCustomBand;
  var PrintBand: Boolean);
begin
  DivisionLabel.Caption := PlayersForm.DBGrid2.Fields[1].Text;
  TotalWeight := 0;
  TotalValue := 0;
  DM1.Player.FindKey([DM1.DateRateQueryPlayerNo.Value]);
  Weighting := DM1.LeagueLatestFrameWeight.Value + 1;
  QRLabel23.Caption := DM1.LeagueExplanation.Value;
end;

procedure TPlayerReport.DetailBand1BeforePrint(Sender: TQRCustomBand;
  var PrintBand: Boolean);
var Value: Integer;
begin
  Weighting := Weighting - DM1.LeagueWeightDrop.Value;
  DM1.AllPlayerLookUp.Open;
  DM1.AllPlayerLookUp.FindKey([DM1.DateRateQueryAgainst.Value]);
  QRLabel6.Caption := DM1.AllPlayerLookUpPlayerName.Value;
  if DM1.DateRateQueryWon.Value = true then
    QRLabel7.Caption := 'Won'
  else
   QRLabel7.Caption := 'Lost';
  if DM1.DateRateQuery.RecNo > DM1.LeagueWeightGames.Value then
    QRLabel9.Caption := '---'
  else
    QRLabel9.Caption := IntToStr(Weighting);
  Value := Weighting * DM1.DateRateQueryRating.Value;
  if DM1.DateRateQuery.RecNo > DM1.LeagueWeightGames.Value then
    QRLabel12.Caption := '---'
  else
    QRLabel12.Caption := IntToStr(Value);
  if DM1.DateRateQuery.RecNo <= DM1.LeagueWeightGames.Value then
  begin
    TotalWeight := TotalWeight + Weighting;
    TotalValue := TotalValue + Value;
  end;
end;

procedure TPlayerReport.SummaryBand1BeforePrint(Sender: TQRCustomBand;
  var PrintBand: Boolean);
begin
  DM1.AllPlayerLookUp.FindKey([DM1.DateRateQueryPlayerNo.Value]);
  QRLabel13.Caption := IntToStr(TotalWeight);
  QRLabel14.Caption := IntToStr(TotalValue);
end;

end.
