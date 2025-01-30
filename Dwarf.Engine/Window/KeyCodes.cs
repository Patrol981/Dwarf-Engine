namespace Dwarf.Windowing;

using SDL3;

public enum Keycode : uint {
  Unknown = SDL_Keycode.Unknown,
  Return = SDL_Keycode.Return,
  Escape = SDL_Keycode.Escape,
  Backspace = SDL_Keycode.Backspace,
  Tab = SDL_Keycode.Tab,
  Space = SDL_Keycode.Space,
  Exclaim = SDL_Keycode.Exclaim,
  Dblapostrophe = SDL_Keycode.Dblapostrophe,
  Hash = SDL_Keycode.Hash,
  Dollar = SDL_Keycode.Dollar,
  Percent = SDL_Keycode.Percent,
  Ampersand = SDL_Keycode.Ampersand,
  Apostrophe = SDL_Keycode.Apostrophe,
  LeftParen = SDL_Keycode.LeftParen,
  RightParen = SDL_Keycode.RightParen,
  Asterisk = SDL_Keycode.Asterisk,
  Plus = SDL_Keycode.Plus,
  Comma = SDL_Keycode.Comma,
  Minus = SDL_Keycode.Minus,
  Period = SDL_Keycode.Period,
  Slash = SDL_Keycode.Slash,
  _0 = SDL_Keycode._0,
  _1 = SDL_Keycode._1,
  _2 = SDL_Keycode._2,
  _3 = SDL_Keycode._3,
  _4 = SDL_Keycode._4,
  _5 = SDL_Keycode._5,
  _6 = SDL_Keycode._6,
  _7 = SDL_Keycode._7,
  _8 = SDL_Keycode._8,
  _9 = SDL_Keycode._9,
  Colon = SDL_Keycode.Colon,
  Semicolon = SDL_Keycode.Semicolon,
  Less = SDL_Keycode.Less,
  Equals = SDL_Keycode.Equals,
  Greater = SDL_Keycode.Greater,
  Question = SDL_Keycode.Question,
  At = SDL_Keycode.At,
  LeftBracket = SDL_Keycode.LeftBracket,
  Backslash = SDL_Keycode.Backslash,
  RightBracket = SDL_Keycode.RightBracket,
  Caret = SDL_Keycode.Caret,
  Underscore = SDL_Keycode.Underscore,
  Grave = SDL_Keycode.Grave,
  A = SDL_Keycode.A,
  B = SDL_Keycode.B,
  C = SDL_Keycode.C,
  D = SDL_Keycode.D,
  E = SDL_Keycode.E,
  F = SDL_Keycode.F,
  G = SDL_Keycode.G,
  H = SDL_Keycode.H,
  I = SDL_Keycode.I,
  J = SDL_Keycode.J,
  K = SDL_Keycode.K,
  L = SDL_Keycode.L,
  M = SDL_Keycode.M,
  N = SDL_Keycode.N,
  O = SDL_Keycode.O,
  P = SDL_Keycode.P,
  Q = SDL_Keycode.Q,
  R = SDL_Keycode.R,
  S = SDL_Keycode.S,
  T = SDL_Keycode.T,
  U = SDL_Keycode.U,
  V = SDL_Keycode.V,
  W = SDL_Keycode.W,
  X = SDL_Keycode.X,
  Y = SDL_Keycode.Y,
  Z = SDL_Keycode.Z,
  Leftbrace = SDL_Keycode.Leftbrace,
  Pipe = SDL_Keycode.Pipe,
  Rightbrace = SDL_Keycode.Rightbrace,
  Tilde = SDL_Keycode.Tilde,
  Delete = SDL_Keycode.Delete,
  PlusMinus = SDL_Keycode.PlusMinus,
  Capslock = SDL_Keycode.Capslock,
  F1 = SDL_Keycode.F1,
  F2 = SDL_Keycode.F2,
  F3 = SDL_Keycode.F3,
  F4 = SDL_Keycode.F4,
  F5 = SDL_Keycode.F5,
  F6 = SDL_Keycode.F6,
  F7 = SDL_Keycode.F7,
  F8 = SDL_Keycode.F8,
  F9 = SDL_Keycode.F9,
  F10 = SDL_Keycode.F10,
  F11 = SDL_Keycode.F11,
  F12 = SDL_Keycode.F12,
  PrintScreen = SDL_Keycode.PrintScreen,
  ScrollLock = SDL_Keycode.ScrollLock,
  Pause = SDL_Keycode.Pause,
  Insert = SDL_Keycode.Insert,
  Home = SDL_Keycode.Home,
  PageUp = SDL_Keycode.PageUp,
  End = SDL_Keycode.End,
  PageDown = SDL_Keycode.PageDown,
  Right = SDL_Keycode.Right,
  Left = SDL_Keycode.Left,
  Down = SDL_Keycode.Down,
  Up = SDL_Keycode.Up,
  NumLockClear = SDL_Keycode.NumLockClear,
  KpDivide = SDL_Keycode.KpDivide,
  KpMultiply = SDL_Keycode.KpMultiply,
  KpMinus = SDL_Keycode.KpMinus,
  KpPlus = SDL_Keycode.KpPlus,
  KpEnter = SDL_Keycode.KpEnter,
  Kp1 = SDL_Keycode.Kp1,
  Kp2 = SDL_Keycode.Kp2,
  Kp3 = SDL_Keycode.Kp3,
  Kp4 = SDL_Keycode.Kp4,
  Kp5 = SDL_Keycode.Kp5,
  Kp6 = SDL_Keycode.Kp6,
  Kp7 = SDL_Keycode.Kp7,
  Kp8 = SDL_Keycode.Kp8,
  Kp9 = SDL_Keycode.Kp9,
  Kp0 = SDL_Keycode.Kp0,
  KpPeriod = SDL_Keycode.KpPeriod,
  Application = SDL_Keycode.Application,
  Power = SDL_Keycode.Power,
  KpEquals = SDL_Keycode.KpEquals,
  F13 = SDL_Keycode.F13,
  F14 = SDL_Keycode.F14,
  F15 = SDL_Keycode.F15,
  F16 = SDL_Keycode.F16,
  F17 = SDL_Keycode.F17,
  F18 = SDL_Keycode.F18,
  F19 = SDL_Keycode.F19,
  F20 = SDL_Keycode.F20,
  F21 = SDL_Keycode.F21,
  F22 = SDL_Keycode.F22,
  F23 = SDL_Keycode.F23,
  F24 = SDL_Keycode.F24,
  Execute = SDL_Keycode.Execute,
  Help = SDL_Keycode.Help,
  Menu = SDL_Keycode.Menu,
  Select = SDL_Keycode.Select,
  Stop = SDL_Keycode.Stop,
  Again = SDL_Keycode.Again,
  Undo = SDL_Keycode.Undo,
  Cut = SDL_Keycode.Cut,
  Copy = SDL_Keycode.Copy,
  Paste = SDL_Keycode.Paste,
  Find = SDL_Keycode.Find,
  Mute = SDL_Keycode.Mute,
  VolumeUp = SDL_Keycode.VolumeUp,
  VolumeDown = SDL_Keycode.VolumeDown,
  KpComma = SDL_Keycode.KpComma,
  KpEqualsas400 = SDL_Keycode.KpEqualsas400,
  Alterase = SDL_Keycode.Alterase,
  Sysreq = SDL_Keycode.Sysreq,
  Cancel = SDL_Keycode.Cancel,
  Clear = SDL_Keycode.Clear,
  Prior = SDL_Keycode.Prior,
  Return2 = SDL_Keycode.Return2,
  Separator = SDL_Keycode.Separator,
  Out = SDL_Keycode.Out,
  Oper = SDL_Keycode.Oper,
  Clearagain = SDL_Keycode.Clearagain,
  Crsel = SDL_Keycode.Crsel,
  Exsel = SDL_Keycode.Exsel,
  Kp00 = SDL_Keycode.Kp00,
  Kp000 = SDL_Keycode.Kp000,
  Thousandsseparator = SDL_Keycode.Thousandsseparator,
  Decimalseparator = SDL_Keycode.Decimalseparator,
  Currencyunit = SDL_Keycode.Currencyunit,
  Currencysubunit = SDL_Keycode.Currencysubunit,
  KpLeftParen = SDL_Keycode.KpLeftParen,
  KpRightParen = SDL_Keycode.KpRightParen,
  KpLeftbrace = SDL_Keycode.KpLeftbrace,
  KpRightbrace = SDL_Keycode.KpRightbrace,
  KpTab = SDL_Keycode.KpTab,
  KpBackspace = SDL_Keycode.KpBackspace,
  KpA = SDL_Keycode.KpA,
  KpB = SDL_Keycode.KpB,
  KpC = SDL_Keycode.KpC,
  KpD = SDL_Keycode.KpD,
  KpE = SDL_Keycode.KpE,
  KpF = SDL_Keycode.KpF,
  KpXor = SDL_Keycode.KpXor,
  KpPower = SDL_Keycode.KpPower,
  KpPercent = SDL_Keycode.KpPercent,
  KpLess = SDL_Keycode.KpLess,
  KpGreater = SDL_Keycode.KpGreater,
  KpAmpersand = SDL_Keycode.KpAmpersand,
  KpDblampersand = SDL_Keycode.KpDblampersand,
  KpVerticalbar = SDL_Keycode.KpVerticalbar,
  KpDblverticalbar = SDL_Keycode.KpDblverticalbar,
  KpColon = SDL_Keycode.KpColon,
  KpHash = SDL_Keycode.KpHash,
  KpSpace = SDL_Keycode.KpSpace,
  KpAt = SDL_Keycode.KpAt,
  KpExclam = SDL_Keycode.KpExclam,
  KpMemstore = SDL_Keycode.KpMemstore,
  KpMemrecall = SDL_Keycode.KpMemrecall,
  KpMemclear = SDL_Keycode.KpMemclear,
  KpMemadd = SDL_Keycode.KpMemadd,
  KpMemsubtract = SDL_Keycode.KpMemsubtract,
  KpMemmultiply = SDL_Keycode.KpMemmultiply,
  KpMemdivide = SDL_Keycode.KpMemdivide,
  KpPlusMinus = SDL_Keycode.KpPlusMinus,
  KpClear = SDL_Keycode.KpClear,
  KpClearentry = SDL_Keycode.KpClearentry,
  KpBinary = SDL_Keycode.KpBinary,
  KpOctal = SDL_Keycode.KpOctal,
  KpDecimal = SDL_Keycode.KpDecimal,
  KpHexadecimal = SDL_Keycode.KpHexadecimal,
  LeftControl = SDL_Keycode.LeftControl,
  LeftShift = SDL_Keycode.LeftShift,
  LeftAlt = SDL_Keycode.LeftAlt,
  LeftGui = SDL_Keycode.LeftGui,
  RightControl = SDL_Keycode.RightControl,
  RightShift = SDL_Keycode.RightShift,
  RightAlt = SDL_Keycode.RightAlt,
  RightGui = SDL_Keycode.RightGui,
  Mode = SDL_Keycode.Mode,
  Sleep = SDL_Keycode.Sleep,
  Wake = SDL_Keycode.Wake,
  ChannelIncrement = SDL_Keycode.ChannelIncrement,
  ChannelDecrement = SDL_Keycode.ChannelDecrement,
  MediaPlay = SDL_Keycode.MediaPlay,
  MediaPause = SDL_Keycode.MediaPause,
  MediaRecord = SDL_Keycode.MediaRecord,
  MediaFastForward = SDL_Keycode.MediaFastForward,
  MediaRewind = SDL_Keycode.MediaRewind,
  MediaNextTrack = SDL_Keycode.MediaNextTrack,
  MediaPreviousTrack = SDL_Keycode.MediaPreviousTrack,
  MediaStop = SDL_Keycode.MediaStop,
  MediaEject = SDL_Keycode.MediaEject,
  MediaPlayPause = SDL_Keycode.MediaPlayPause,
  MediaSelect = SDL_Keycode.MediaSelect,
  AcNew = SDL_Keycode.AcNew,
  AcOpen = SDL_Keycode.AcOpen,
  AcClose = SDL_Keycode.AcClose,
  AcExit = SDL_Keycode.AcExit,
  AcSave = SDL_Keycode.AcSave,
  AcPrint = SDL_Keycode.AcPrint,
  AcProperties = SDL_Keycode.AcProperties,
  AcSearch = SDL_Keycode.AcSearch,
  AcHome = SDL_Keycode.AcHome,
  AcBack = SDL_Keycode.AcBack,
  AcForward = SDL_Keycode.AcForward,
  AcStop = SDL_Keycode.AcStop,
  AcRefresh = SDL_Keycode.AcRefresh,
  AcBookmarks = SDL_Keycode.AcBookmarks,
  Softleft = SDL_Keycode.Softleft,
  Softright = SDL_Keycode.Softright,
  Call = SDL_Keycode.Call,
  Endcall = SDL_Keycode.Endcall,
}
