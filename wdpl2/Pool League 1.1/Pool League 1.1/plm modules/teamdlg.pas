unit teamdlg;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls, 
  Buttons, ExtCtrls, Grids, DBGrids;

type
  TTeamDialog = class(TForm)
    OKBtn: TButton;
    CancelBtn: TButton;
    Grid: TDBGrid;
    procedure GridDblClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  TeamDialog: TTeamDialog;

implementation

uses datamodule;

{$R *.DFM}

procedure TTeamDialog.GridDblClick(Sender: TObject);
begin
  ModalResult := mrOK;
end;

end.
