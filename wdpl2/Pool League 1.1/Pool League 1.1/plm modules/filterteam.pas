unit filterteam;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls, 
  Buttons, ExtCtrls, DBCtrls;

type
  TFilterDlg = class(TForm)
    OKBtn: TButton;
    CancelBtn: TButton;
    ListBox1: TListBox;
    ListBox2: TListBox;
    procedure FormShow(Sender: TObject);
    procedure ListBox1DblClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  FilterDlg: TFilterDlg;

implementation

uses datamodule;
{$R *.DFM}

procedure TFilterDlg.FormShow(Sender: TObject);
begin
  ListBox1.Clear;
  ListBox2.Clear;
  DM1.TeamByName.Close;
  DM1.TeamByName.Open;
  DM1.TeamByName.First;
  while not DM1.TeamByName.EOF do
  begin
    ListBox1.Items.Add(DM1.TeamByNameTeamName.Value);
    ListBox2.Items.Add(IntToStr(DM1.TeamByNameItem_id.Value));
    DM1.TeamByName.Next;
  end;
  ListBox1.ItemIndex := 0;
end;

procedure TFilterDlg.ListBox1DblClick(Sender: TObject);
begin
  ModalResult := mrOK;
end;

end.
