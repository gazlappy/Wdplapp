{=====================================================================
  Copyright 1993-1996 by Teletech Systems, Inc. All rights reserved


This source code may not be distributed in part or as a whole without
express written permission from Teletech Systems.
=====================================================================}
unit HelpDefs;
interface



{ To add help to your application, add the statement
  
  Application.Helpfile := 'plm.hlp';
  
  to your project startup file (*.DPR).
  
  [F1] key help will work automatically, if you allowed HelpWriter to 
  update your code. }
{=====================================================================
 List of Context IDs for <plm>
 =====================================================================}
 const
      Hlp_About_ = 10;    {Main Help Window}
      Hlp_Admin4Pool_ = 30;    {Main Help Window}
      Hlp_New_Division = 420;    {Main Help Window}
      Hlp_Divisions_ = 430;    {Main Help Window}
      Hlp_Select_League = 440;    {Main Help Window}
      Hlp_Players_ = 450;    {Main Help Window}
      Hlp_Amend_Player = 460;    {Main Help Window}
      Hlp_League_Properties = 470;    {Main Help Window}
      Hlp_Team_ = 480;    {Main Help Window}
      Hlp_Team_List = 490;    {Main Help Window}
      Hlp_Update_ = 500;    {Main Help Window}
      Hlp_New_Venue = 510;    {Main Help Window}
      Hlp_Venues_ = 520;    {Main Help Window}
      Hlp_RatingReport_ = 550;    {Main Help Window}
      Hlp_TableReport_ = 560;    {Main Help Window}
      Hlp_DoublesReport_ = 570;    {Main Help Window}
      Hlp_PlayerReport_ = 580;    {Main Help Window}
      Hlp_EightBallReport_ = 590;    {Main Help Window}
      Hlp_ResultReport_ = 600;    {Main Help Window}
      Hlp_Mailshot_ = 610;    {Main Help Window}
      Hlp_Select_Team = 620;    {Main Help Window}
      Hlp_CovletVenue_ = 630;    {Main Help Window}
      Hlp_CovletCaptain_ = 640;    {Main Help Window}
      Hlp_Web_Pages = 650;    {Main Help Window}
      Hlp_VenueLblRpt_ = 660;    {Main Help Window}
      Hlp_CaptainLblRpt_ = 670;    {Main Help Window}
      Hlp_Away_Players = 680;    {Main Help Window}
      Hlp_Home_Players = 690;    {Main Help Window}
      Hlp_Home_Players1 = 700;    {Main Help Window}
      Hlp_Away_Players1 = 710;    {Main Help Window}
      Hlp_New_League = 730;    {Main Help Window}
      Hlp_Open_League = 740;    {Main Help Window}
      Hlp_SplashForm_ = 750;    {Main Help Window}
      Hlp_Back_Up = 760;    {Main Help Window}
 {Glossary definitions}


implementation

end.

