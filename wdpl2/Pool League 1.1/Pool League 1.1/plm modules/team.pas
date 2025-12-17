unit Team;

interface

uses WinTypes, WinProcs, Classes, Graphics, Forms, Controls, Buttons,
  StdCtrls, DBLookup, Mask, DBCtrls, DB, DBTables, ExtCtrls;

type
  TTeamsForm = class(TForm)
    OKBtn: TBitBtn;
    Bevel1: TBevel;
    DBEdit1: TDBEdit;
    Label1: TLabel;
    Label2: TLabel;
    Label3: TLabel;
    Label4: TLabel;
    Label5: TLabel;
    DBEdit2: TDBEdit;
    DBEdit3: TDBEdit;
    DBEdit4: TDBEdit;
    DBEdit5: TDBEdit;
    DBEdit6: TDBEdit;
    DBLookupCombo1: TDBLookupCombo;
    DBLookupCombo2: TDBLookupCombo;
    BitBtn1: TBitBtn;
    GroupBox1: TGroupBox;
    Label6: TLabel;
    DBEdit7: TDBEdit;
    Label7: TLabel;
    DBEdit8: TDBEdit;
    Label8: TLabel;
    DBEdit9: TDBEdit;
    Label10: TLabel;
    Label11: TLabel;
    Label9: TLabel;
    DBEdit10: TDBEdit;
    SpeedButton1: TSpeedButton;
    SpeedButton2: TSpeedButton;
    DBCheckBox1: TDBCheckBox;
    DBCheckBox2: TDBCheckBox;
    procedure SpeedButton1Click(Sender: TObject);
    procedure OKBtnClick(Sender: TObject);
    procedure BitBtn1Click(Sender: TObject);
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
    procedure DBEdit8Exit(Sender: TObject);
    procedure SpeedButton2Click(Sender: TObject);
  private
    { Private declarations }
  public
    procedure Enter;
    procedure Edit(Item_id: Integer);
    { Public declarations }
  end;

var
  TeamsForm: TTeamsForm;

implementation

uses venue, Main, Venins, Teaml, datamodule, Division;

{$R *.DFM}

procedure TTeamsForm.Enter;
begin
  DM1.Team.Insert;
  DM1.Team_1.Insert;
  ActiveControl := DBEdit1;
  ShowModal;
end;

procedure TTeamsForm.Edit(Item_id: Integer);
begin
  DM1.Team.FindKey([Item_id]);
  DM1.Team.Edit;
  if DM1.Team_1.FindKey([Item_id]) then
    DM1.Team_1.Edit
  else
    DM1.Team_1.Insert;
  ActiveControl := DBEdit1;
  ShowModal;
end;

procedure TTeamsForm.SpeedButton1Click(Sender: TObject);
begin
  VenueInsert.ShowModal;
end;

procedure TTeamsForm.OKBtnClick(Sender: TObject);
begin
  DM1.Team.Post;
  try
    DM1.Team_1Item_id.Value := DM1.TeamItem_id.Value;
    DM1.Team_1Deduction.Value := DM1.Team_1Deduction.Value + 0;
    DM1.Team_1.Post;
  except
  end;
  DM1.Team.Refresh;
  ModalResult := mrOK;
end;

procedure TTeamsForm.BitBtn1Click(Sender: TObject);
begin
  DM1.Team.Cancel;
  DM1.Team_1.Cancel;
  ModalResult := mrCancel;
end;

procedure TTeamsForm.FormClose(Sender: TObject; var Action: TCloseAction);
begin
  TeamList.Enabled := True;
end;

procedure TTeamsForm.DBEdit8Exit(Sender: TObject);
begin
  DM1.Team_1FinesDue.Value := DM1.Team_1AmtFined.Value - DM1.Team_1FinesPaid.Value;
end;

procedure TTeamsForm.SpeedButton2Click(Sender: TObject);
begin
  DivisionsForm.ShowModal;
end;

end.
