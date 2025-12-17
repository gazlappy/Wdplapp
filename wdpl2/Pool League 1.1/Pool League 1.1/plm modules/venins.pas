unit Venins;

interface

uses WinTypes, WinProcs, Classes, Graphics, Forms, Controls, Buttons,
  StdCtrls, DB, DBTables, ExtCtrls;

type
  TVenueInsert = class(TForm)
    OKBtn: TBitBtn;
    CancelBtn: TBitBtn;
    Venue: TLabel;
    Address: TLabel;
    Edit1: TEdit;
    Edit2: TEdit;
    Edit3: TEdit;
    Edit4: TEdit;
    Edit5: TEdit;
    procedure OKBtnClick(Sender: TObject);
    procedure CancelBtnClick(Sender: TObject);
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
  private
    { Private declarations }
  public
    procedure Clear;
    { Public declarations }
  end;

var
  VenueInsert: TVenueInsert;

implementation

uses Venue, Team, datamodule;

{$R *.DFM}

procedure TVenueInsert.Clear;
begin
  ActiveControl := Edit1;
  Edit1.Clear;
  Edit2.Clear;
  Edit3.Clear;
  Edit4.Clear;
  Edit5.Clear;
end;

procedure TVenueInsert.OKBtnClick(Sender: TObject);
begin
  if Edit1.Text <> '' then
  begin
    DM1.Venue.Insert;
    DM1.VenueVenue.Value := Edit1.Text;
    DM1.VenueAddressLine1.Value := Edit2.Text;
    DM1.VenueAddressLine2.Value := Edit3.Text;
    DM1.VenueAddressLine3.Value := Edit4.Text;
    DM1.VenueAddressLine4.Value := Edit5.Text;
    DM1.Venue.Post;
  end;
  ModalResult := mrOK;
end;

procedure TVenueInsert.CancelBtnClick(Sender: TObject);
begin
  ModalResult := mrCancel;
end;

procedure TVenueInsert.FormClose(Sender: TObject;
  var Action: TCloseAction);
begin
  try
    VenuesForm.Enabled := True;
    DM1.Venue.Refresh;
  except
  end;
  try
    TeamsForm.Enabled := True;
  except
  end;
end;

end.
