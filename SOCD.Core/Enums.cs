namespace SOCD.Core;

/// <summary>SOCD 清除模式</summary>
public enum SocdMode
{
    /// <summary>中立模式：同時按下相反方向時相互抵銷（不輸出）</summary>
    Neutral,
    /// <summary>後按優先：同時按下相反方向時，以最新按下的鍵優先輸出</summary>
    LastInputPriority,
    /// <summary>先按優先：同時按下相反方向時，以最先按下的鍵持續輸出，新鍵被吞掉</summary>
    FirstInputPriority
}

/// <summary>方向旗標</summary>
[Flags]
public enum Direction
{
    None  = 0,
    Up    = 1,
    Down  = 2,
    Left  = 4,
    Right = 8
}

/// <summary>監聽之目標按鍵</summary>
public enum GameKey
{
    W = 0x57,
    A = 0x41,
    S = 0x53,
    D = 0x44,
    Ctrl = 0x11
}

/// <summary>合成按鍵動作</summary>
public readonly record struct KeyAction(GameKey Key, bool IsDown);
