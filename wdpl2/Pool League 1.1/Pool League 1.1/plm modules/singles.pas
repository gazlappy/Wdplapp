unit Singles;

interface

uses WinTypes, WinProcs, Classes, Graphics, Forms, Controls, Buttons,
  StdCtrls, DBCtrls, DB, DBTables, Grids, DBGrids, DBLookup, ExtCtrls;

type
  TSinglesForm = class(TForm)
    OKBtn: TBitBtn;
    CancelBtn: TBitBtn;
    HelpBtn: TBitBtn;
    GroupBox1: TGroupBox;
    DBText1: TDBText;
    Label1: TLabel;
    DBText2: TDBText;
    Label2: TLabel;
    Label3: TLabel;
    Label4: TLabel;
    Label5: TLabel;
    DBListBox1: TDBListBox;
    DBNavigator1: TDBNavigator;
    DBLookupCombo1: TDBLookupCombo;
    Player: TTable;
    HomeList: TQuery;
    PlayerSource: TDataSource;
    Single: TTable;
    SingleSource: TDataSource;
    HomeTeamInd: TLabel;
    procedure FormCreate(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
    CarryMatchNo: Double;
    CarryHomeTeam: String;
    CarryAwayTeam: String;
  end;

var
  SinglesForm: TSinglesForm;

implementation

uses main,match;
{$R *.DFM}

procedure TSinglesForm.FormCreate(Sender: TObject);
begin
  Single.Open;
  Player.Open;
  HomeTeamInd.Caption := CarryHomeTeam
end;

end.
