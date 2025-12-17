unit openleague;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls, 
  Buttons, ExtCtrls;

type
  TOpenLeagueDlg = class(TForm)
    Bevel1: TBevel;
    ListBox1: TListBox;
    BitBtn1: TBitBtn;
    BitBtn2: TBitBtn;
    procedure FormShow(Sender: TObject);
    procedure OKBtnClick(Sender: TObject);
  private
    { Private declarations }
  public
    UniRefs: Array of Integer;
    NewLeague: Boolean;
    { Public declarations }
  end;

var
  OpenLeagueDlg: TOpenLeagueDlg;

implementation

uses datamodule, datamodule2, newleague;

{$R *.DFM}

procedure TOpenLeagueDlg.FormShow(Sender: TObject);
var dummy: Integer;
begin
  SetLength(UniRefs,999);
  ListBox1.Items.Clear;
  DM2.a_League.Open;
  DM2.a_League.First;
  dummy := 0;
  while not DM2.a_League.EOF do
  begin
    ListBox1.Items.Add(DM2.a_LeagueLeagueName.Value + ' ' + DM2.a_LeagueSeason.Value);
    UniRefs[dummy] := DM2.a_LeagueUniRef.Value;
    DM2.a_League.Next;
    dummy := dummy + 1;
  end;
  ListBox1.ItemIndex := 0;
end;

procedure TOpenLeagueDlg.OKBtnClick(Sender: TObject);
begin
  NewLeague := False;
  NewLeagueDlg.MoveRecords(NewLeague);
end;

end.
