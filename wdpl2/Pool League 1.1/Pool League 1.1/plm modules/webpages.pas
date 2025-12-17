unit webpages;

interface

uses Windows, SysUtils, Classes, Graphics, Forms, Controls, StdCtrls, 
  Buttons, ExtCtrls, Dialogs;

type
  TWebPageDlg = class(TForm)
    Label1: TLabel;
    Label2: TLabel;
    Edit1: TEdit;
    Edit2: TEdit;
    OKBtn: TBitBtn;
    CancelBtn: TBitBtn;
    procedure FormShow(Sender: TObject);
    procedure outputline;
    procedure WebPageHeading;
    procedure PlayerPageHeading;
    procedure WebResults;
    procedure WebTables;
    procedure WebSinglesRatings;
    procedure WebDoublesRatings;
    procedure WebPlayerReports;
    procedure Button1Click(Sender: TObject);
    procedure Button2Click(Sender: TObject);
  private
    { Private declarations }
  public
    F: textfile;
    G: textfile;
    page_count: Integer;
    outline: String;
    webfilename: String;
    subdirectory: String;
    bodytypestarttag, bodytypeendtag, htmlstarttag, htmlendtag: String;
    headstarttag, headendtag, titlestarttag, titleendtag, est, eet: String;
    bodystarttag, bodyendtag, tablestarttag, tableendtag, estright: String;
    rowstarttag, rowendtag, pageheading, textstarttag, textendtag: String;
    { Public declarations }
  end;

var
  WebPageDlg: TWebPageDlg;

implementation

uses Main, datamodule;

{$R *.DFM}

procedure TWebPageDlg.FormShow(Sender: TObject);
begin
  subdirectory := DateToStr(Date);
  subdirectory := StringReplace(subdirectory, '/' , '-',[ rfReplaceAll ]);
  subdirectory := 'webpages' + subdirectory;
  Edit1.Text := DM1.Database.Directory + subdirectory;
  Edit2.Text := '';
  WebPageDlg.Refresh;
  page_count := 0;
//
end;

procedure TWebPageDlg.outputline;
begin
//Showmessage(outline);
  Writeln(F, outline);
end;

procedure TWebPageDlg.WebPageHeading;
begin
  page_count := page_count + 1;
  Edit2.Text := IntToStr(page_count);
  Edit2.Refresh;
  bodytypestarttag := '<P ALIGN="LEFT"><FONT FACE="ARIAL" SIZE="6" COLOR="#458B00">';
  bodytypeendtag := '</FONT></P>';
  textstarttag := '<P ALIGN="LEFT"><FONT FACE="ARIAL" SIZE="5" COLOR="#458B00">';
  textendtag := '</FONT></P>';
  htmlstarttag := '<HTML>';
  htmlendtag := '</HTML>';
  headstarttag := '<HEAD>';
  headendtag := '</HEAD><style>A:hover {color:green;}</style>';
  titlestarttag := '<TITLE>';
  titleendtag := '</TITLE>';
  bodystarttag := '<BODY BGCOLOR=#fee8c6>';
  bodyendtag := '</BODY>';
  tablestarttag := '<TABLE BORDER=1 CELLPADDING=3 CELLSPACING=0 BORDERCOLOR=#008000>';
  tableendtag := '</TABLE>';
  rowstarttag := '<TR BGCOLOR=#C1FFC1>';
  rowendtag := '</TR>';
  est := '<TD><P><FONT FACE="ARIAL">';
  estright := '<TD ALIGN="RIGHT"><P><FONT FACE="ARIAL">';
  eet := '</FONT></P></TD>';
  outline := htmlstarttag;
  outputline;
  outline := headstarttag;
  outputline;
  outline := titlestarttag + DM1.LeagueLeagueName.Value + titleendtag;
  outputline;
  outline := headendtag;
  outputline;
  outline := bodystarttag;
  outputline;
  outline := bodytypestarttag + pageheading + bodytypeendtag;
  outputline;
  outline := textstarttag + 'Last Update: ' + DateToStr(Date) + textendtag;
  outputline;
end;

procedure TWebPageDlg.PlayerPageHeading;
begin
  page_count := page_count + 1;
  Edit2.Text := IntToStr(page_count);
  Edit2.Refresh;
  bodytypestarttag := '<P ALIGN="LEFT"><FONT FACE="ARIAL" SIZE="6" COLOR="#458B00">';
  bodytypeendtag := '</FONT></P>';
  textstarttag := '<P ALIGN="LEFT"><FONT FACE="ARIAL" SIZE="5" COLOR="#458B00">';
  textendtag := '</FONT></P>';
  htmlstarttag := '<HTML>';
  htmlendtag := '</HTML>';
  headstarttag := '<HEAD>';
  headendtag := '</HEAD><style>A:hover {color:green;}</style>';
  titlestarttag := '<TITLE>';
  titleendtag := '</TITLE>';
  bodystarttag := '<BODY BGCOLOR=#fee8c6>';
  bodyendtag := '</BODY>';
  tablestarttag := '<TABLE BORDER=1 CELLPADDING=3 CELLSPACING=0 BORDERCOLOR=#008000>';
  tableendtag := '</TABLE>';
  rowstarttag := '<TR BGCOLOR=#C1FFC1>';
  rowendtag := '</TR>';
  est := '<TD><P><FONT FACE="ARIAL">';
  estright := '<TD ALIGN="RIGHT"><P><FONT FACE="ARIAL">';
  eet := '</FONT></P></TD>';
  outline := htmlstarttag;
  Writeln(G, outline);
  outline := headstarttag;
  Writeln(G, outline);
  outline := titlestarttag + DM1.LeagueLeagueName.Value + titleendtag;
  Writeln(G, outline);
  outline := headendtag;
  Writeln(G, outline);
  outline := bodystarttag;
  Writeln(G, outline);
  outline := bodytypestarttag + pageheading + bodytypeendtag;
  Writeln(G, outline);
  outline := textstarttag + 'Last Update: ' + DateToStr(Date) + textendtag;
  Writeln(G, outline);
end;

procedure TWebPageDlg.WebPlayerReports;
var playercount, Value, Weighting, TotalWeight, TotalValue: Integer;
begin
  playercount := 0;
  webfilename := DM1.Database.Directory + subdirectory + '\players.htm';
  AssignFile(G, webfilename);
  try
    Rewrite(G)
  except
  end;
  pageheading := 'List of Players';
  PlayerPageHeading;
  DM1.PlayerQueryByName.Close;
  DM1.PlayerQueryByName.Open;
  DM1.PlayerQueryByName.First;
  outline := '<P ALIGN="CENTER">';
  writeln(G, outline);
  while not DM1.PlayerQueryByName.EOF do
  begin
    // add player to player list, 8 across the page
    playercount := playercount + 1;
    if playercount = 1 then
      outline := '<BR>';
    outline := outline + '<A HREF="player' + FloatToStr(DM1.PlayerQueryByNamePlayerNo.Value) + '.htm">';
    outline := outline + DM1.PlayerQueryByNamePlayerName.Value + '</A>&nbsp;&nbsp;&nbsp;';
    if playercount = 8 then
      playercount := 0;
    writeln(G, outline);
    webfilename := DM1.Database.Directory + subdirectory + '\player' + FloatToStr(DM1.PlayerQueryByNamePlayerNo.Value) + '.htm';
    AssignFile(F, webfilename);
    try
      Rewrite(F)
    except
    end;
    pageheading := 'Record of ' + DM1.PlayerQueryByNamePlayerName.Value + ' (' + DM1.PlayerQueryByNamePlayerTeamName.Value + ')';
    WebPageHeading;
    outline := tablestarttag;
    outputline;
    outline := rowstarttag;
    outputline;
    outline := est + 'Played' + eet;
    outputline;
    outline := est + 'Won' + eet;
    outputline;
    outline := est + 'Lost' + eet;
    outputline;
    outline := est + 'Eight<BR>Balls' + eet;
    outputline;
    outline := est + 'Best<BR>Rating' + eet;
    outputline;
    outline := est + 'Attained<BR>On' + eet;
    outputline;
    outline := est + 'Current<BR>Rating' + eet;
    outputline;
    outline := rowendtag;
    outputline;
    outline := rowstarttag;
    outputline;
    outline := estright + IntToStr(DM1.PlayerQueryByNamePlayed.Value) + eet;
    outputline;
    outline := estright + IntToStr(DM1.PlayerQueryByNameWins.Value) + eet;
    outputline;
    outline := estright + IntToStr(DM1.PlayerQueryByNameLosses.Value) + eet;
    outputline;
    outline := estright + IntToStr(DM1.PlayerQueryByNameEightBalls.Value) + eet;
    outputline;
    outline := estright + IntToStr(DM1.PlayerQueryByNameBestRating.Value) + eet;
    outputline;
    outline := estright + DateToStr(DM1.PlayerQueryByNameBestRatingDate.Value) + eet;
    outputline;
    outline := estright + IntToStr(DM1.PlayerQueryByNameCurrentRating.Value) + eet;
    outputline;
    outline := rowendtag;
    outputline;
    outline := tableendtag;
    outputline;
    outline := textstarttag + 'Full Record' + textendtag;
    outputline;
//create full player record
    DM1.DateRateQuery.Close;
    DM1.DateRateQuery.Params.ParamByName('SubPlayerNo').AsFloat := DM1.PlayerQueryByNamePlayerNo.Value;
    DM1.DateRateQuery.Open;
    Weighting := DM1.LeagueLatestFrameWeight.Value + 1;
    TotalWeight := 0;
    TotalValue := 0;
    outline := tablestarttag;
    outputline;
    outline := rowstarttag;
    outputline;
    outline := est + 'Match<BR>Date' + eet;
    outputline;
    outline := est + 'Opponent' + eet;
    outputline;
    outline := est + 'Team' + eet;
    outputline;
    outline := est + 'Result' + eet;
    outputline;
    outline := estright + 'Rating<BR>Attained' + eet;
    outputline;
    outline := estright + 'Weighting' + eet;
    outputline;
    outline := estright + 'Value' + eet;
    outputline;
    outline := rowendtag;
    outputline;
    while not DM1.DateRateQuery.EOF do
    begin
      Weighting := Weighting - DM1.LeagueWeightDrop.Value;
      DM1.AllPlayerLookUp.Open;
      DM1.AllPlayerLookUp.FindKey([DM1.DateRateQueryAgainst.Value]);
      Value := Weighting * DM1.DateRateQueryRating.Value;
      TotalWeight := TotalWeight + Weighting;
      TotalValue := TotalValue + Value;
      outline := rowstarttag;
      outputline;
      outline := est + DateToStr(DM1.DateRateQueryRatingDate.Value) + eet;
      outputline;
//      outline := est + PlayerReport.PlayerPlayerName.Value + eet;
      outline := '<TD><P><FONT FACE="ARIAL"><A HREF="player' + FloatToStr(DM1.AllPlayerLookUpPlayerNo.Value) + '.htm">';
      outline := outline + DM1.AllPlayerLookUpPlayerName.Value + '</A></FONT></P></TD>';
      outputline;
      outline := est + DM1.AllPlayerLookUpPlayerTeamName.Value + eet;
      outputline;
      if DM1.DateRateQueryWon.Value = true then
        outline := est + 'Won' + eet
      else
        outline := est + 'Lost' + eet;
      outputline;
      outline := estright + IntToStr(DM1.DateRateQueryRating.Value) + eet;
      outputline;
      outline := estright + IntToStr(Weighting) + eet;
      outputline;
      outline := estright + IntToStr(Value) + eet;
      outputline;
      outline := rowendtag;
      outputline;
      DM1.DateRateQuery.Next;
    end;
// total row
    outline := rowstarttag;
    outputline;
    outline := estright + ' ' + eet;
    outputline;
    outline := estright + ' ' + eet;
    outputline;
    outline := estright + ' ' + eet;
    outputline;
    outline := estright + ' ' + eet;
    outputline;
    outline := estright + 'Totals ' + eet;
    outputline;
    outline := estright + IntToStr(TotalWeight) + eet;
    outputline;
    outline := estright + IntToStr(TotalValue) + eet;
    outputline;
    outline := rowendtag;
    outputline;
//end the page
    outline := tableendtag;
    outputline;
    outline := '<P>Total Value / Total Weighting = Current Rating</P>';
    outputline;
    outline := bodyendtag;
    outputline;
    outline := htmlendtag;
    outputline;
    Closefile(F);
    DM1.PlayerQueryByName.Next;
  end;
  outline := '</P>';
  writeln(G, outline);
  outline := bodyendtag;
  writeln(G, outline);
  outline := htmlendtag;
  writeln(G, outline);
  Closefile(G);
end;


procedure TWebPageDlg.WebDoublesRatings;
begin
  DM1.Division.Open;
  DM1.Division.First;
  while not DM1.Division.EOF do
  begin
    webfilename := DM1.Database.Directory + subdirectory + '\double' + DM1.DivisionAbbreviated.Value + '.htm';
    AssignFile(F, webfilename);
    try
      Rewrite(F)
    except
    end;
    pageheading := DM1.DivisionFullDivisionName.Value + ' Doubles Ratings';
    WebPageHeading;
    outline := tablestarttag;
    outputline;
    outline := rowstarttag;
    outputline;
    outline := est + 'Pos' + eet;
    outputline;
    outline := est + 'Player<BR>No.' + eet;
    outputline;
    outline := est + 'Player Name' + eet;
    outputline;
    outline := est + 'Player<BR>No.' + eet;
    outputline;
    outline := est + 'Player Name' + eet;
    outputline;
    outline := est + 'Team Name' + eet;
    outputline;
    outline := est + 'Played' + eet;
    outputline;
    outline := est + 'Won' + eet;
    outputline;
    outline := est + 'Lost' + eet;
    outputline;
    outline := est + 'Best<BR>Rating' + eet;
    outputline;
    outline := est + 'Attained<BR>On' + eet;
    outputline;
    outline := est + 'Current<BR>Rating' + eet;
    outputline;
    outline := rowendtag;
    outputline;
    DM1.PairQuery.Close;
    DM1.PairQuery.Params.ParamByName('SelectedDiv').AsInteger := DM1.DivisionItem_id.Value;
    DM1.PairQuery.Open;
    while not DM1.PairQuery.EOF do
    begin
      outline := rowstarttag;
      outputline;
      outline := estright + IntToStr(DM1.PairQuery.RecNo) + eet;
      outputline;
      outline := est + FloatToStr(DM1.PairQueryPlayerNo1.Value) + eet;
      outputline;
      outline := est + DM1.PairQueryPlayerName1.Value + eet;
      outputline;
      outline := est + FloatToStr(DM1.PairQueryPlayerNo2.Value) + eet;
      outputline;
      outline := est + DM1.PairQueryPlayerName2.Value + eet;
      outputline;
      outline := est + DM1.PairQueryPlayerTeamName.Value + eet;
      outputline;
      outline := estright + IntToStr(DM1.PairQueryPlayed.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.PairQueryWins.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.PairQueryLosses.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.PairQueryBestRating.Value) + eet;
      outputline;
      outline := estright + DateToStr(DM1.PairQueryBestRatingDate.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.PairQueryCurrentRating.Value) + eet;
      outputline;
      outline := rowendtag;
      outputline;
      DM1.PairQuery.Next;
    end;
    outline := tableendtag;
    outputline;
    outline := bodyendtag;
    outputline;
    outline := htmlendtag;
    outputline;
    Closefile(F);
    DM1.Division.Next;
  end;
end;

procedure TWebPageDlg.WebSinglesRatings;
begin
  DM1.Division.Open;
  DM1.Division.First;
  while not DM1.Division.EOF do
  begin
    webfilename := DM1.Database.Directory + subdirectory + '\single' + DM1.DivisionAbbreviated.Value + '.htm';
    AssignFile(F, webfilename);
    try
      Rewrite(F)
    except
    end;
    pageheading := DM1.DivisionFullDivisionName.Value + ' Player Ratings';
    WebPageHeading;
    outline := tablestarttag;
    outputline;
    outline := rowstarttag;
    outputline;
    outline := est + 'Pos' + eet;
    outputline;
    outline := est + 'Player<BR>No.' + eet;
    outputline;
    outline := est + 'Player Name' + eet;
    outputline;
    outline := est + 'Team Name' + eet;
    outputline;
    outline := est + 'Played' + eet;
    outputline;
    outline := est + 'Won' + eet;
    outputline;
    outline := est + 'Lost' + eet;
    outputline;
    outline := est + '8 Balls' + eet;
    outputline;
    outline := est + 'Best<BR>Rating' + eet;
    outputline;
    outline := est + 'Attained<BR>On' + eet;
    outputline;
    outline := est + 'Current<BR>Rating' + eet;
    outputline;
    outline := rowendtag;
    outputline;
    DM1.PlayerQuery.Close;
    DM1.PlayerQuery.Params.ParamByName('SelectedDiv').AsInteger := DM1.DivisionItem_id.Value;
    DM1.PlayerQuery.Open;
    while not DM1.PlayerQuery.EOF do
    begin
      outline := rowstarttag;
      outputline;
      outline := estright + IntToStr(DM1.PlayerQuery.RecNo) + eet;
      outputline;
      outline := est + FloatToStr(DM1.PlayerQueryPlayerNo.Value) + eet;
      outputline;
      outline := '<TD><P><FONT FACE="ARIAL"><A HREF="player' + FloatToStr(DM1.PlayerQueryPlayerNo.Value) + '.htm">';
      outline := outline + DM1.PlayerQueryPlayerName.Value + '</A></FONT></P></TD>';
      outputline;
      outline := est + DM1.PlayerQueryPlayerTeamName.Value + eet;
      outputline;
      outline := estright + IntToStr(DM1.PlayerQueryPlayed.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.PlayerQueryWins.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.PlayerQueryLosses.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.PlayerQueryEightBalls.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.PlayerQueryBestRating.Value) + eet;
      outputline;
      outline := estright + DateToStr(DM1.PlayerQueryBestRatingDate.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.PlayerQueryCurrentRating.Value) + eet;
      outputline;
      outline := rowendtag;
      outputline;
      DM1.PlayerQuery.Next;
    end;
    outline := tableendtag;
    outputline;
    outline := bodyendtag;
    outputline;
    outline := htmlendtag;
    outputline;
    Closefile(F);
    DM1.Division.Next;
  end;
end;

procedure TWebPageDlg.WebTables;
begin
  DM1.Division.First;
  while not DM1.Division.EOF do
  begin
    webfilename := DM1.Database.Directory + subdirectory + '\table' + DM1.DivisionAbbreviated.Value + '.htm';
    AssignFile(F, webfilename);
    Rewrite(F);
    pageheading := DM1.DivisionFullDivisionName.Value + ' Table';
    WebPageHeading;
    outline := tablestarttag;
    outputline;
    outline := rowstarttag;
    outputline;
    outline := est + 'Pos' + eet;
    outputline;
    outline := est + 'Team Name' + eet;
    outputline;
    outline := est + 'Fines<BR>Due' + eet;
    outputline;
    outline := est + 'Played' + eet;
    outputline;
    outline := est + 'Won' + eet;
    outputline;
    outline := est + 'Lost' + eet;
    outputline;
    outline := est + 'Drawn' + eet;
    outputline;
    outline := est + 'Singles<BR>Won' + eet;
    outputline;
    outline := est + 'Singles<BR>Lost' + eet;
    outputline;
    outline := est + 'Doubles<BR>Won' + eet;
    outputline;
    outline := est + 'Doubles<BR>Lost' + eet;
    outputline;
    outline := est + 'Points<BR>Deducted' + eet;
    outputline;
    outline := est + 'Points' + eet;
    outputline;
    outline := rowendtag;
    outputline;
    DM1.TeamQuery.Close;
    DM1.TeamQuery.Params.ParamByName('SelectedDiv').AsInteger := DM1.DivisionItem_id.Value;
    DM1.TeamQuery.Open;
    while not DM1.TeamQuery.EOF do
    begin
      outline := rowstarttag;
      outputline;
      outline := estright + IntToStr(DM1.TeamQuery.RecNo) + eet;
      outputline;
      outline := est + DM1.TeamQueryTeamName.Value + eet;
      outputline;
      outline := estright + FloatToStr(DM1.TeamQueryFinesDue.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.TeamQueryPlayed.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.TeamQueryWins.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.TeamQueryLoses.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.TeamQueryDraws.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.TeamQuerySWins.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.TeamQuerySLosses.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.TeamQueryDWins.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.TeamQueryDLosses.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.TeamQueryDeduction.Value) + eet;
      outputline;
      outline := estright + IntToStr(DM1.TeamQueryNett.Value) + eet;
      outputline;
      outline := rowendtag;
      outputline;
      DM1.TeamQuery.Next;
    end;
    outline := tableendtag;
    outputline;
    outline := bodyendtag;
    outputline;
    outline := htmlendtag;
    outputline;
    Closefile(F);
    DM1.Division.Next;
  end;
end;

procedure TWebPageDlg.WebResults;
var matchresult: String;
var hometotal, awaytotal: Double;
begin
// Stage 1 - RESULTS
  AssignFile(F, webfilename);
  try
    Rewrite(F);
  except
  end;
  pageheading := 'Results';
  WebPageHeading;
  outline := tablestarttag;
  outputline;
  outline := rowstarttag;
  outputline;
  outline := est + 'Date' + eet;
  outputline;
  outline := est + 'Div' + eet;
  outputline;
  outline := est + 'Home Team' + eet;
  outputline;
  outline := est + 'Single<BR>Wins' + eet;
  outputline;
  outline := est + 'Double<BR>Wins' + eet;
  outputline;
  outline := est + 'Points' + eet;
  outputline;
  outline := est + 'Result' + eet;
  outputline;
  outline := est + 'Away Team' + eet;
  outputline;
  outline := est + 'Single<BR>Wins' + eet;
  outputline;
  outline := est + 'Double<BR>Wins' + eet;
  outputline;
  outline := est + 'Points' + eet;
  outputline;
  outline := rowendtag;
  outputline;
  DM1.MatchQuery.Close;
  DM1.MatchQuery.Params.ParamByName('SelectedDate').AsDate := StrToDate('01/01/1900');
  DM1.MatchQuery.Open;
  while not DM1.MatchQuery.EOF do
  begin
    outline := rowstarttag;
    outputline;
    outline := est + DateToStr(DM1.MatchQueryMatchDate.Value) + eet;
    outputline;
    outline := est + DM1.MatchQueryDivName.Value + eet;
    outputline;
    outline := est + DM1.MatchQueryHomeTeamName.Value + eet;
    outputline;
    outline := estright + FloatToStr(DM1.MatchQueryHSWins.Value) + eet;
    outputline;
    outline := estright + FloatToStr(DM1.MatchQueryHDWins.Value) + eet;
    outputline;
    hometotal := DM1.MatchQueryHSWins.Value * DM1.LeagueSinglesBonus.Value;
    hometotal := hometotal + (DM1.MatchQueryHDWins.Value * DM1.LeagueDoublesBonus.Value);
    awaytotal := DM1.MatchQueryASWins.Value * DM1.LeagueSinglesBonus.Value;
    awaytotal := awaytotal + (DM1.MatchQueryADWins.Value * DM1.LeagueDoublesBonus.Value);
    if hometotal > awaytotal then
    begin
      hometotal := hometotal + DM1.LeagueWinBonus.Value;
      awaytotal := awaytotal + DM1.LeagueLossBonus.Value;
      matchresult := 'beat'
    end;
    if hometotal = awaytotal then
    begin
      hometotal := hometotal + DM1.LeagueDrawBonus.Value;
      awaytotal := awaytotal + DM1.LeagueDrawBonus.Value;
      matchresult := 'drew with'
    end;
    if hometotal < awaytotal then
    begin
      hometotal := hometotal + DM1.LeagueLossBonus.Value;
      awaytotal := awaytotal + DM1.LeagueWinBonus.Value;
      matchresult := 'lost to'
    end;
    outline := estright + FloatToStr(hometotal) + eet;
    outputline;
    outline := est + matchresult + eet;
    outputline;
    outline := est + DM1.MatchQueryAwayTeamName.Value + eet;
    outputline;
    outline := estright + FloatToStr(DM1.MatchQueryASWins.Value) + eet;
    outputline;
    outline := estright + FloatToStr(DM1.MatchQueryADWins.Value) + eet;
    outputline;
    outline := estright + FloatToStr(awaytotal) + eet;
    outputline;
    outline := rowendtag;
    outputline;
    DM1.MatchQuery.Next;
  end;
  outline := tableendtag;
  outputline;
  outline := bodyendtag;
  outputline;
  outline := htmlendtag;
  outputline;
  Closefile(F);
end;

procedure TWebPageDlg.Button1Click(Sender: TObject);
begin
{$I+}
// CD to the webpages directory to check it exists
  webfilename := DM1.Database.Directory + subdirectory;
  try
    chdir(webfilename);
  except
// must create directory
    try
      MkDir(webfilename);
      MessageDlg(webfilename + ' directory created', mtInformation, [mbOk], 0);
    except
      MessageDlg('Cannot create directory', mtWarning, [mbOk], 0);
      Abort;
    end;
  end;
// Creates Web Page Document (html) for
// 1.  Complete Results
  webfilename := DM1.Database.Directory + subdirectory + '\results.htm';
  WebResults;
// 2.  Tables
  WebTables;
// 3.  Singles Ratings by division
  WebSinglesRatings;
// 4.  Doubles Ratings by division
  WebDoublesRatings;
// 5.  Player Reports
  WebPlayerReports;
  Close;
end;

procedure TWebPageDlg.Button2Click(Sender: TObject);
begin
  Close;
end;

end.
