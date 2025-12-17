unit Pninsert;

interface

uses WinTypes, WinProcs, Classes, Graphics, Forms, Controls, Buttons,
  StdCtrls, DB, DBTables, ExtCtrls, Mask, DBCtrls;

type
  TPlayerAmend = class(TForm)
    OKBtn: TBitBtn;
    CancelBtn: TBitBtn;
    Bevel1: TBevel;
    Label1: TLabel;
    Player: TTable;
    PlayerSource: TDataSource;
    DBEdit1: TDBEdit;
    PlayerPlayerName: TStringField;
    PlayerPlayerNo: TFloatField;
    procedure OKBtnClick(Sender: TObject);
  private
    { Private declarations }
  public
    procedure Edit(PlayerNo: Double);
    { Public declarations }
  end;

var
  PlayerAmend: TPlayerAmend;

implementation

{$R *.DFM}

procedure TPlayerAmend.Edit(PlayerNo: Double);
begin
  Player.Open;
  Player.FindKey([PlayerNo]);
  Player.Edit;
  ActiveControl := DBEdit1;
  ShowModal;
end;

procedure TPlayerAmend.OKBtnClick(Sender: TObject);
begin
  Player.Post;
  Player.Close;
  Close;
end;

end.
