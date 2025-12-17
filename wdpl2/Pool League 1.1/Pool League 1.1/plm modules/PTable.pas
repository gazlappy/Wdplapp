unit PTable;

interface

uses Windows, SysUtils, Messages, Classes, Graphics, Controls,
  StdCtrls, ExtCtrls, Forms, Quickrpt, QRCtrls, Db, DBTables, dialogs;

type
  TTableReport = class(TQuickRep)
    ColumnHeaderBand1: TQRBand;
    DetailBand1: TQRBand;
    QRDBText2: TQRDBText;
    QRLabel1: TQRLabel;
    Played: TQRLabel;
    QRDBText3: TQRDBText;
    Won: TQRLabel;
    QRDBText4: TQRDBText;
    Lost: TQRLabel;
    QRDBText5: TQRDBText;
    Drawn: TQRLabel;
    QRDBText6: TQRDBText;
    Singles: TQRLabel;
    SWon: TQRLabel;
    SLost: TQRLabel;
    QRDBText7: TQRDBText;
    Doubles: TQRLabel;
    DWon: TQRLabel;
    DLost: TQRLabel;
    QRDBText8: TQRDBText;
    Points: TQRLabel;
    QRSysData2: TQRSysData;
    QRDBText11: TQRDBText;
    QRDBText12: TQRDBText;
    QRLabel6: TQRLabel;
    QRLabel7: TQRLabel;
    QRDBText10: TQRDBText;
    QRDBText13: TQRDBText;
    QRDBText9: TQRDBText;
    QRLabel9: TQRLabel;
    QRLabel10: TQRLabel;
    QRDBText14: TQRDBText;
    TitleBand1: TQRBand;
    QRShape1: TQRShape;
    QRLabel25: TQRLabel;
    QRShape2: TQRShape;
    QRLabel26: TQRLabel;
    QRLabel27: TQRLabel;
    QRSysData3: TQRSysData;
    QRShape3: TQRShape;
    QRDBText15: TQRDBText;
    DivisionLabel: TQRLabel;
    QRLabel28: TQRLabel;
    procedure TitleBand1BeforePrint(Sender: TQRCustomBand;
      var PrintBand: Boolean);
  private

  public

  end;

var
  TableReport: TTableReport;

implementation

uses datamodule;
{$R *.DFM}

procedure TTableReport.TitleBand1BeforePrint(Sender: TQRCustomBand;
  var PrintBand: Boolean);
begin
  if DM1.Division.FindKey([DM1.TeamQueryDivision.Value]) then
    DivisionLabel.Caption := DM1.DivisionFullDivisionName.Value;
  DivisionLabel.Caption := DM1.LeagueSeason.Value + ' ' + DivisionLabel.Caption;
end;

end.
