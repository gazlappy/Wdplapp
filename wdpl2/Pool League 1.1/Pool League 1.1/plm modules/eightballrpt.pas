unit eightballrpt;

interface

uses Windows, SysUtils, Messages, Classes, Graphics, Controls,
  StdCtrls, ExtCtrls, Forms, Quickrpt, QRCtrls, DBTables, Db;

type
  TEightBallReport = class(TQuickRep)
    ColumnHeaderBand1: TQRBand;
    DetailBand1: TQRBand;
    QRLabel4: TQRLabel;
    QRLabel3: TQRLabel;
    QRLabel5: TQRLabel;
    QRLabel6: TQRLabel;
    QRDBText3: TQRDBText;
    QRDBText2: TQRDBText;
    QRDBText4: TQRDBText;
    QRDBText10: TQRDBText;
    TitleBand1: TQRBand;
    QRShape1: TQRShape;
    QRLabel25: TQRLabel;
    QRShape2: TQRShape;
    QRLabel26: TQRLabel;
    QRLabel27: TQRLabel;
    QRSysData3: TQRSysData;
    QRShape3: TQRShape;
    QRDBText15: TQRDBText;
    QRLabel28: TQRLabel;
    procedure QuickRepBeforePrint(Sender: TCustomQuickRep;
      var PrintReport: Boolean);
  private

  public

  end;

var
  EightBallReport: TEightBallReport;

implementation

uses datamodule;

{$R *.DFM}

procedure TEightBallReport.QuickRepBeforePrint(Sender: TCustomQuickRep;
  var PrintReport: Boolean);
begin
  DM1.EBQuery.Close;
  DM1.EBQuery.Open;
  DM1.EBQuery.First;
end;

end.
