unit plmmailshot;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls, 
  Buttons, ExtCtrls, ComCtrls, Db, DBTables, Mask;

type
  TMailshot = class(TForm)
    Label1: TLabel;
    VenueQuery: TQuery;
    VenueQueryVenue: TStringField;
    VenueQueryAddressLine1: TStringField;
    VenueQueryAddressLine2: TStringField;
    VenueQueryAddressLine3: TStringField;
    VenueQueryAddressLine4: TStringField;
    TeamQuery1: TQuery;
    TeamQuery2: TQuery;
    TeamQuery2Contact: TStringField;
    TeamQuery2ContactAddress1: TStringField;
    TeamQuery2ContactAddress2: TStringField;
    TeamQuery2ContactAddress3: TStringField;
    TeamQuery2ContactAddress4: TStringField;
    BitBtn1: TBitBtn;
    BitBtn2: TBitBtn;
    SpeedButton1: TSpeedButton;
    Label2: TLabel;
    RadioGroup1: TRadioGroup;
    RadioGroup2: TRadioGroup;
    TeamQuery1Withdrawn: TBooleanField;
    TeamQuery2Withdrawn: TBooleanField;
    VenueQueryItem_id: TIntegerField;
    TeamQuery2Venue: TIntegerField;
    TeamQuery2Division: TIntegerField;
    TeamQuery1Venue: TIntegerField;
    TeamQuery1Division: TIntegerField;
    procedure FormShow(Sender: TObject);
    procedure OKBtnClick(Sender: TObject);
    procedure CancelBtnClick(Sender: TObject);
    procedure SpeedButton1Click(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  Mailshot: TMailshot;

implementation

uses covlet, PRating, DRatings, PTable, covlet2, resrpt, datamodule, pickdt,
  venuelabel, captainlabel;

{$R *.DFM}

procedure TMailshot.FormShow(Sender: TObject);
begin
  Label2.Caption := DateToStr(Date - 28);
end;

procedure TMailshot.OKBtnClick(Sender: TObject);
begin
//produces Table Report, Singles Report, Doubles Report and
//Results Report for each Team Captain and Venue
//Get date into Result SQL
  DM1.MatchQuery.Close;
  DM1.MatchQuery.Params.ParamByName('SelectedDate').AsDate := StrToDate(Label2.Caption);
// Check for Labels only
  if RadioGroup1.ItemIndex = 1 then
  begin
    if RadioGroup2.ItemIndex <> 1 then VenueLblRpt.Preview;
    if RadioGroup2.ItemIndex <> 2 then CaptainLblRpt.Preview;
    Exit;
  end;
  if RadioGroup2.ItemIndex <> 1 then
    begin
    VenueQuery.Close;
    VenueQuery.Open;
    VenueQuery.First;
    while not VenueQuery.EOF do
    begin
      CovletVenue.Preview;
      if RadioGroup1.ItemIndex <> 0 then
      begin
        TeamQuery1.Close;
        TeamQuery1.Params.ParamByName('Venue').AsInteger := VenueQueryItem_id.Value;
        TeamQuery1.Open;
        TeamQuery1.First;
        while not TeamQuery1.EOF do
        begin
          DM1.MatchQuery.Open;
          DM1.PlayerQuery.Close;
          DM1.PlayerQuery.Params.ParamByName('SelectedDiv').AsInteger := TeamQuery1Division.Value;
          DM1.PlayerQuery.Open;
          DM1.PairQuery.Close;
          DM1.PairQuery.Params.ParamByName('SelectedDiv').AsInteger := TeamQuery1Division.Value;
          DM1.PairQuery.Open;
          DM1.TeamQuery.Close;
          DM1.TeamQuery.Params.ParamByName('SelectedDiv').AsInteger := TeamQuery1Division.Value;
          DM1.TeamQuery.Open;
          ResultReport.Preview;
          TableReport.Preview;
          RatingReport.Preview;
          DoublesReport.Preview;
          TeamQuery1.Next;
        end;
      end;
      VenueQuery.Next;
    end;
  end;
  if RadioGroup2.ItemIndex <> 2 then
    begin
    TeamQuery2.Close;
    TeamQuery2.Open;
    TeamQuery2.First;
    while not TeamQuery2.EOF do
    begin
      CovletCaptain.Preview;
      if RadioGroup1.ItemIndex <> 0 then
      begin
        DM1.MatchQuery.Open;
        DM1.PlayerQuery.Close;
        DM1.PlayerQuery.Params.ParamByName('SelectedDiv').AsInteger := TeamQuery2Division.Value;
        DM1.PlayerQuery.Open;
        DM1.PairQuery.Close;
        DM1.PairQuery.Params.ParamByName('SelectedDiv').AsInteger := TeamQuery2Division.Value;
        DM1.PairQuery.Open;
        DM1.TeamQuery.Close;
        DM1.TeamQuery.Params.ParamByName('SelectedDiv').AsInteger := TeamQuery2Division.Value;
        DM1.TeamQuery.Open;
        ResultReport.Preview;
        TableReport.Preview;
        RatingReport.Preview;
        DoublesReport.Preview;
      end;
      TeamQuery2.Next;
    end;
  end;
  ModalResult := mrOK;
end;

procedure TMailshot.CancelBtnClick(Sender: TObject);
begin
  ModalResult := mrCancel;
end;

procedure TMailshot.SpeedButton1Click(Sender: TObject);
begin
  if PickDate.ShowModal = mrOK then
    Label2.Caption := DateToStr(PickDate.Calendar1.CalendarDate);
end;


end.
