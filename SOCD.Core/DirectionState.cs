namespace SOCD.Core;

/// <summary>
/// 按鍵狀態快照
/// </summary>
public readonly record struct KeyStateSnapshot(
    bool W,
    bool A,
    bool S,
    bool D,
    bool Ctrl
)
{
    public Direction AsDirection()
    {
        var dir = Direction.None;
        if (W) dir |= Direction.Up;
        if (S) dir |= Direction.Down;
        if (Left()) dir |= Direction.Left;
        if (Right()) dir |= Direction.Right;
        return dir;
    }

    private bool Left() => A;
    private bool Right() => D;
}
