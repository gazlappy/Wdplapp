object BackUpForm: TBackUpForm
  Left = 342
  Top = 239
  Width = 253
  Height = 202
  BorderIcons = []
  Caption = 'Back Up Data'
  Color = clBtnFace
  Font.Charset = DEFAULT_CHARSET
  Font.Color = clWindowText
  Font.Height = -11
  Font.Name = 'MS Sans Serif'
  Font.Style = []
  OldCreateOrder = False
  Position = poOwnerFormCenter
  PixelsPerInch = 96
  TextHeight = 13
  object Label1: TLabel
    Left = 38
    Top = 8
    Width = 168
    Height = 13
    Caption = 'Do you wish to back up your data ?'
  end
  object Label2: TLabel
    Left = 24
    Top = 69
    Width = 16
    Height = 13
    Caption = 'File'
  end
  object Label3: TLabel
    Left = 24
    Top = 97
    Width = 54
    Height = 13
    Caption = 'Size (bytes)'
  end
  object Label4: TLabel
    Left = 24
    Top = 114
    Width = 111
    Height = 13
    Caption = 'Available Space (bytes)'
  end
  object Label5: TLabel
    Left = 64
    Top = 69
    Width = 165
    Height = 20
    Alignment = taRightJustify
    AutoSize = False
  end
  object Label6: TLabel
    Left = 160
    Top = 97
    Width = 72
    Height = 13
    Alignment = taRightJustify
    AutoSize = False
  end
  object Label7: TLabel
    Left = 152
    Top = 114
    Width = 80
    Height = 13
    Alignment = taRightJustify
    AutoSize = False
  end
  object Button1: TButton
    Left = 24
    Top = 32
    Width = 75
    Height = 25
    Caption = 'to A:\'
    TabOrder = 0
    OnClick = Button1Click
  end
  object Button2: TButton
    Left = 144
    Top = 32
    Width = 75
    Height = 25
    Caption = 'to C:'
    TabOrder = 1
    OnClick = Button2Click
  end
  object Button3: TButton
    Left = 85
    Top = 144
    Width = 75
    Height = 25
    Caption = 'Exit'
    TabOrder = 2
    OnClick = Button3Click
  end
end
