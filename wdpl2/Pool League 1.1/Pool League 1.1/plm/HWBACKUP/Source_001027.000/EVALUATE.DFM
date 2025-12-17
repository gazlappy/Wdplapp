object Form2: TForm2
  Left = 192
  Top = 107
  Width = 535
  Height = 335
  BorderIcons = []
  Caption = 'Software4Sport Licence'
  Color = clBtnFace
  Font.Charset = DEFAULT_CHARSET
  Font.Color = clWindowText
  Font.Height = -11
  Font.Name = 'MS Sans Serif'
  Font.Style = []
  OldCreateOrder = False
  Position = poScreenCenter
  PixelsPerInch = 96
  TextHeight = 13
  object Label1: TLabel
    Left = 130
    Top = 246
    Width = 69
    Height = 13
    Caption = 'Serial Number:'
  end
  object DBText1: TDBText
    Left = 210
    Top = 246
    Width = 65
    Height = 17
    DataField = 'SerialNo'
    DataSource = LS
  end
  object Label2: TLabel
    Left = 276
    Top = 246
    Width = 55
    Height = 13
    Caption = 'Lock Code:'
  end
  object DBText2: TDBText
    Left = 344
    Top = 246
    Width = 65
    Height = 17
    DataField = 'LockCode'
    DataSource = LS
  end
  object Memo1: TMemo
    Left = 8
    Top = 8
    Width = 513
    Height = 225
    Color = clBtnFace
    Lines.Strings = (
      'Thank you for your interest in Pool League Manager.'
      ''
      
        'Your software needs to be licenced.  If you are an existing user' +
        ', you can extend your licence within 28 days '
      
        'of the current expiry date.  For continual use of this product, ' +
        'you must re-licence.'
      ''
      
        'If you are evaluating this software product, Software4Sport prov' +
        'ide a free 28 day evaluation period.  '
      
        'However, we would first like you to register your interest in th' +
        'is product.  Once you have done so, we will '
      
        'return to you a licence key and you may qualify for an extended ' +
        '3 month evaluation period.'
      ''
      
        'In either case, please email your name, address and any organisa' +
        'tion you represent to '
      
        'simon@software4sport.freeserve.co.uk.  You will also need to inc' +
        'lude your serial number and licence lock '
      'code which are presented below.'
      ''
      
        'After you click OK, you will be given an opportunity to enter yo' +
        'ur licence key.  If you are within 28 days of'
      
        'your current expiry date, you may click '#39'Cancel'#39' to continue usi' +
        'ng the software without applying a licence'
      'extension.')
    TabOrder = 0
  end
  object OKBtn: TBitBtn
    Left = 228
    Top = 270
    Width = 69
    Height = 33
    Font.Charset = DEFAULT_CHARSET
    Font.Color = clBlack
    Font.Height = -11
    Font.Name = 'MS Sans Serif'
    Font.Style = []
    ParentFont = False
    TabOrder = 1
    OnClick = Button1Click
    Kind = bkOK
    Margin = 2
    Spacing = -1
    IsControl = True
  end
  object LS: TDataSource
    DataSet = Form1.Licence
    Left = 8
    Top = 272
  end
end
