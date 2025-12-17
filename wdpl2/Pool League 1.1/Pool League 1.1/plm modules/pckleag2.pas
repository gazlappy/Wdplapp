unit pckleag2;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls, 
  Buttons, ExtCtrls, Db, DBTables;

type
  TPickLeague2 = class(TForm)
    CancelButton: TBitBtn;
    PrintButton: TButton;
    PreviewButton: TButton;
    ListBox1: TListBox;
    DivQuery: TQuery;
    DivQueryAbbreviated: TStringField;
    procedure PreviewButtonClick(Sender: TObject);
    procedure FormShow(Sender: TObject);
    procedure PrintButtonClick(Sender: TObject);
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
    procedure CancelButtonClick(Sender: TObject);
    procedure ListBox1Exit(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  PickLeague2: TPickLeague2;

implementation

uses Main, DRatings;

{$R *.DFM}

procedure TPickLeague2.PreviewButtonClick(Sender: TObject);
begin
  DoublesReport.Preview;
  Close;
end;

procedure TPickLeague2.FormShow(Sender: TObject);
begin
  ListBox1.Clear;
  DivQuery.Close;
  DivQuery.Open;
  DivQuery.First;
  while not DivQuery.EOF do
  begin
    ListBox1.Items.Add(DivQueryAbbreviated.Value);
    DivQuery.Next;
  end;
  ListBox1.ItemIndex := 0;
end;

procedure TPickLeague2.PrintButtonClick(Sender: TObject);
begin
  DoublesReport.Print;
  Close;
end;

procedure TPickLeague2.FormClose(Sender: TObject; var Action: TCloseAction);
begin
  Form1.EnableMenus;
  Action := caFree;
end;

procedure TPickLeague2.CancelButtonClick(Sender: TObject);
begin
  Close;
end;

procedure TPickLeague2.ListBox1Exit(Sender: TObject);
begin
  DoublesReport.PairQuery.Close;
  DoublesReport.PairQuery.Params.ParamByName('SelectedDiv').AsString := ListBox1.Items.Strings[ListBox1.ItemIndex];
  DoublesReport.PairQuery.Open;
end;

{$R *.DFM}

end.
