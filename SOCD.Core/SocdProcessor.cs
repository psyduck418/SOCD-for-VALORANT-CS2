namespace SOCD.Core;

/// <summary>
/// SOCD 處理核心引擎，負責追蹤實體按鍵、計算目標輸出狀態，並產生精確的合成按鍵動作 (KeyAction)。
/// 與 UI 完全解耦，具備 100% 可單元測試特性。
/// </summary>
public class SocdProcessor
{
    // 實體按鍵狀態 (Physical Pressed)
    private bool _physW, _physA, _physS, _physD, _physCtrl;

    // 邏輯輸出狀態 (目前在作業系統中被判定為按下中的按鍵)
    private bool _outW, _outA, _outS, _outD, _outCtrl;

    // 時序追蹤 (用於最後輸入優先與先按優先)
    private GameKey? _lastVertical;    // 最後按下的垂直鍵
    private GameKey? _firstVertical;   // 最先按下的垂直鍵
    private GameKey? _lastHorizontal;  // 最後按下的水平鍵
    private GameKey? _firstHorizontal; // 最先按下的水平鍵

    // Ctrl vs WASD 互斥優先序標記: true 表示最後互動是 Ctrl，false 表示最後互動是 WASD
    private bool _ctrlHasPriorityOverWasd;

    public SocdMode Mode { get; set; } = SocdMode.Neutral;
    public bool EnableCtrlPriority { get; set; } = false;

    public KeyStateSnapshot PhysicalState => new(_physW, _physA, _physS, _physD, _physCtrl);
    public KeyStateSnapshot LogicalOutputState => new(_outW, _outA, _outS, _outD, _outCtrl);

    public Direction CleanedDirection => LogicalOutputState.AsDirection();

    /// <summary>
    /// 處理實體鍵盤事件，回傳需要發送給系統的合成按鍵清單 (SendInput)。
    /// </summary>
    public List<KeyAction> ProcessKey(GameKey key, bool isDown)
    {
        // 1. 更新實體按鍵狀態與時序
        UpdatePhysicalState(key, isDown);

        // 2. 計算期望的邏輯輸出狀態 (Target Outputs)
        var (targetW, targetA, targetS, targetD, targetCtrl) = CalculateTargetOutputs();

        // 3. 比對目前輸出與目標輸出，產生最小化按鍵事件清單 (優先釋放 KeyUp，再按下 KeyDown)
        var actions = new List<KeyAction>();

        // 處理 KeyUp
        if (_outW && !targetW) actions.Add(new KeyAction(GameKey.W, false));
        if (_outA && !targetA) actions.Add(new KeyAction(GameKey.A, false));
        if (_outS && !targetS) actions.Add(new KeyAction(GameKey.S, false));
        if (_outD && !targetD) actions.Add(new KeyAction(GameKey.D, false));
        if (_outCtrl && !targetCtrl) actions.Add(new KeyAction(GameKey.Ctrl, false));

        // 處理 KeyDown
        if (!_outW && targetW) actions.Add(new KeyAction(GameKey.W, true));
        if (!_outA && targetA) actions.Add(new KeyAction(GameKey.A, true));
        if (!_outS && targetS) actions.Add(new KeyAction(GameKey.S, true));
        if (!_outD && targetD) actions.Add(new KeyAction(GameKey.D, true));
        if (!_outCtrl && targetCtrl) actions.Add(new KeyAction(GameKey.Ctrl, true));

        // 4. 更新內部目前輸出狀態
        _outW = targetW;
        _outA = targetA;
        _outS = targetS;
        _outD = targetD;
        _outCtrl = targetCtrl;

        return actions;
    }

    private void UpdatePhysicalState(GameKey key, bool isDown)
    {
        switch (key)
        {
            case GameKey.W:
                if (isDown)
                {
                    _physW = true;
                    _lastVertical = GameKey.W;
                    if (!_physS) _firstVertical = GameKey.W;
                    _ctrlHasPriorityOverWasd = false; // WASD 被按下，壓過 Ctrl
                }
                else
                {
                    _physW = false;
                    if (_physS)
                    {
                        _firstVertical = GameKey.S;
                        _lastVertical = GameKey.S;
                    }
                    else
                    {
                        _firstVertical = null;
                        _lastVertical = null;
                    }
                }
                break;

            case GameKey.S:
                if (isDown)
                {
                    _physS = true;
                    _lastVertical = GameKey.S;
                    if (!_physW) _firstVertical = GameKey.S;
                    _ctrlHasPriorityOverWasd = false;
                }
                else
                {
                    _physS = false;
                    if (_physW)
                    {
                        _firstVertical = GameKey.W;
                        _lastVertical = GameKey.W;
                    }
                    else
                    {
                        _firstVertical = null;
                        _lastVertical = null;
                    }
                }
                break;

            case GameKey.A:
                if (isDown)
                {
                    _physA = true;
                    _lastHorizontal = GameKey.A;
                    if (!_physD) _firstHorizontal = GameKey.A;
                    _ctrlHasPriorityOverWasd = false;
                }
                else
                {
                    _physA = false;
                    if (_physD)
                    {
                        _firstHorizontal = GameKey.D;
                        _lastHorizontal = GameKey.D;
                    }
                    else
                    {
                        _firstHorizontal = null;
                        _lastHorizontal = null;
                    }
                }
                break;

            case GameKey.D:
                if (isDown)
                {
                    _physD = true;
                    _lastHorizontal = GameKey.D;
                    if (!_physA) _firstHorizontal = GameKey.D;
                    _ctrlHasPriorityOverWasd = false;
                }
                else
                {
                    _physD = false;
                    if (_physA)
                    {
                        _firstHorizontal = GameKey.A;
                        _lastHorizontal = GameKey.A;
                    }
                    else
                    {
                        _firstHorizontal = null;
                        _lastHorizontal = null;
                    }
                }
                break;

            case GameKey.Ctrl:
                if (isDown)
                {
                    _physCtrl = true;
                    _ctrlHasPriorityOverWasd = true; // Ctrl 被按下，壓過 WASD
                }
                else
                {
                    _physCtrl = false;
                    _ctrlHasPriorityOverWasd = false;
                }
                break;
        }

        // 若所有 WASD 均已放開，且 Ctrl 仍按住，則恢復 Ctrl 優先狀態
        bool anyWasdPhys = _physW || _physA || _physS || _physD;
        if (!anyWasdPhys && _physCtrl)
        {
            _ctrlHasPriorityOverWasd = true;
        }
    }

    private (bool W, bool A, bool S, bool D, bool Ctrl) CalculateTargetOutputs()
    {
        // 1. 先計算基本 SOCD 方向決策
        bool targetW = false;
        bool targetS = false;
        bool targetA = false;
        bool targetD = false;
        bool targetCtrl = _physCtrl;

        // --- 垂直軸 (W vs S) ---
        if (_physW && _physS)
        {
            switch (Mode)
            {
                case SocdMode.Neutral:
                    targetW = false;
                    targetS = false;
                    break;
                case SocdMode.LastInputPriority:
                    targetW = (_lastVertical == GameKey.W);
                    targetS = (_lastVertical == GameKey.S);
                    break;
                case SocdMode.FirstInputPriority:
                    targetW = (_firstVertical == GameKey.W);
                    targetS = (_firstVertical == GameKey.S);
                    break;
            }
        }
        else
        {
            targetW = _physW;
            targetS = _physS;
        }

        // --- 水平軸 (A vs D) ---
        if (_physA && _physD)
        {
            switch (Mode)
            {
                case SocdMode.Neutral:
                    targetA = false;
                    targetD = false;
                    break;
                case SocdMode.LastInputPriority:
                    targetA = (_lastHorizontal == GameKey.A);
                    targetD = (_lastHorizontal == GameKey.D);
                    break;
                case SocdMode.FirstInputPriority:
                    targetA = (_firstHorizontal == GameKey.A);
                    targetD = (_firstHorizontal == GameKey.D);
                    break;
            }
        }
        else
        {
            targetA = _physA;
            targetD = _physD;
        }

        // 2. 若啟用 Ctrl 優先/互斥功能 (EnableCtrlPriority)
        if (EnableCtrlPriority)
        {
            bool anyWasdPhys = _physW || _physA || _physS || _physD;
            if (_physCtrl && anyWasdPhys)
            {
                if (_ctrlHasPriorityOverWasd)
                {
                    // Ctrl 壓制 WASD: WASD 全部不輸出，Ctrl 正常輸出
                    targetW = false;
                    targetA = false;
                    targetS = false;
                    targetD = false;
                    targetCtrl = true;
                }
                else
                {
                    // WASD 壓制 Ctrl: Ctrl 不輸出，WASD 正常輸出
                    targetCtrl = false;
                }
            }
            else if (_physCtrl)
            {
                targetCtrl = true;
            }
            else if (anyWasdPhys)
            {
                targetCtrl = false;
            }
        }

        return (targetW, targetA, targetS, targetD, targetCtrl);
    }

    /// <summary>
    /// 重置所有狀態（當 Hook 關閉或切換時調用）
    /// </summary>
    public List<KeyAction> Reset()
    {
        var actions = new List<KeyAction>();
        if (_outW) actions.Add(new KeyAction(GameKey.W, false));
        if (_outA) actions.Add(new KeyAction(GameKey.A, false));
        if (_outS) actions.Add(new KeyAction(GameKey.S, false));
        if (_outD) actions.Add(new KeyAction(GameKey.D, false));
        if (_outCtrl) actions.Add(new KeyAction(GameKey.Ctrl, false));

        _physW = _physA = _physS = _physD = _physCtrl = false;
        _outW = _outA = _outS = _outD = _outCtrl = false;
        _lastVertical = _firstVertical = null;
        _lastHorizontal = _firstHorizontal = null;
        _ctrlHasPriorityOverWasd = false;

        return actions;
    }
}
