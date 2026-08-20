using SOCD.Core;
using Xunit;

namespace SOCD.Tests;

public class SocdProcessorTests
{
    private readonly SocdProcessor _proc = new();

    // ────────────────────────────────────────────────────────
    // 1. 中立模式 (Neutral)
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Neutral_PressA_ThenPressD_CancelsBoth()
    {
        _proc.Mode = SocdMode.Neutral;

        // Press A -> A should be Down
        var act1 = _proc.ProcessKey(GameKey.A, true);
        Assert.Single(act1);
        Assert.Equal(new KeyAction(GameKey.A, true), act1[0]);
        Assert.True(_proc.LogicalOutputState.A);

        // Press D -> A should be Released (KeyUp), D should NOT be pressed (or suppressed)
        var act2 = _proc.ProcessKey(GameKey.D, true);
        Assert.Single(act2);
        Assert.Equal(new KeyAction(GameKey.A, false), act2[0]);
        Assert.False(_proc.LogicalOutputState.A);
        Assert.False(_proc.LogicalOutputState.D);
        Assert.Equal(Direction.None, _proc.CleanedDirection);

        // Release D -> A should be Restored (KeyDown)
        var act3 = _proc.ProcessKey(GameKey.D, false);
        Assert.Single(act3);
        Assert.Equal(new KeyAction(GameKey.A, true), act3[0]);
        Assert.True(_proc.LogicalOutputState.A);

        // Release A -> A should be Released
        var act4 = _proc.ProcessKey(GameKey.A, false);
        Assert.Single(act4);
        Assert.Equal(new KeyAction(GameKey.A, false), act4[0]);
        Assert.False(_proc.LogicalOutputState.A);
    }

    [Fact]
    public void Neutral_PressW_ThenPressS_CancelsBoth()
    {
        _proc.Mode = SocdMode.Neutral;

        _proc.ProcessKey(GameKey.W, true);
        Assert.True(_proc.LogicalOutputState.W);

        var acts = _proc.ProcessKey(GameKey.S, true);
        Assert.Single(acts);
        Assert.Equal(new KeyAction(GameKey.W, false), acts[0]);
        Assert.False(_proc.LogicalOutputState.W);
        Assert.False(_proc.LogicalOutputState.S);

        var acts2 = _proc.ProcessKey(GameKey.W, false);
        Assert.Single(acts2);
        Assert.Equal(new KeyAction(GameKey.S, true), acts2[0]);
        Assert.True(_proc.LogicalOutputState.S);
    }

    // ────────────────────────────────────────────────────────
    // 2. 後按優先 (Last Input Priority / 2IP)
    // ────────────────────────────────────────────────────────

    [Fact]
    public void LastInput_PressA_ThenPressD_OutputsD()
    {
        _proc.Mode = SocdMode.LastInputPriority;

        // Press A
        _proc.ProcessKey(GameKey.A, true);
        Assert.True(_proc.LogicalOutputState.A);

        // Press D -> A must release (KeyUp), D must press (KeyDown)
        var acts = _proc.ProcessKey(GameKey.D, true);
        Assert.Equal(2, acts.Count);
        Assert.Equal(new KeyAction(GameKey.A, false), acts[0]);
        Assert.Equal(new KeyAction(GameKey.D, true), acts[1]);
        Assert.False(_proc.LogicalOutputState.A);
        Assert.True(_proc.LogicalOutputState.D);

        // Release D -> D must release, A must restore (KeyDown)
        var acts2 = _proc.ProcessKey(GameKey.D, false);
        Assert.Equal(2, acts2.Count);
        Assert.Equal(new KeyAction(GameKey.D, false), acts2[0]);
        Assert.Equal(new KeyAction(GameKey.A, true), acts2[1]);
        Assert.True(_proc.LogicalOutputState.A);
        Assert.False(_proc.LogicalOutputState.D);

        // Release A
        var acts3 = _proc.ProcessKey(GameKey.A, false);
        Assert.Single(acts3);
        Assert.Equal(new KeyAction(GameKey.A, false), acts3[0]);
        Assert.False(_proc.LogicalOutputState.A);
    }

    [Fact]
    public void LastInput_PressW_ThenPressS_OutputsS()
    {
        _proc.Mode = SocdMode.LastInputPriority;

        _proc.ProcessKey(GameKey.W, true);
        var acts = _proc.ProcessKey(GameKey.S, true);

        Assert.Equal(new KeyAction(GameKey.W, false), acts[0]);
        Assert.Equal(new KeyAction(GameKey.S, true), acts[1]);
        Assert.True(_proc.LogicalOutputState.S);
        Assert.False(_proc.LogicalOutputState.W);
    }

    // ────────────────────────────────────────────────────────
    // 3. 先按優先 (First Input Priority)
    // ────────────────────────────────────────────────────────

    [Fact]
    public void FirstInput_PressA_ThenPressD_KeepsA_BlocksD()
    {
        _proc.Mode = SocdMode.FirstInputPriority;

        // Press A
        _proc.ProcessKey(GameKey.A, true);
        Assert.True(_proc.LogicalOutputState.A);

        // Press D -> D is blocked, no actions generated, A remains active
        var acts = _proc.ProcessKey(GameKey.D, true);
        Assert.Empty(acts);
        Assert.True(_proc.LogicalOutputState.A);
        Assert.False(_proc.LogicalOutputState.D);

        // Release A (while D is still held) -> A releases, D activates!
        var acts2 = _proc.ProcessKey(GameKey.A, false);
        Assert.Equal(2, acts2.Count);
        Assert.Equal(new KeyAction(GameKey.A, false), acts2[0]);
        Assert.Equal(new KeyAction(GameKey.D, true), acts2[1]);
        Assert.False(_proc.LogicalOutputState.A);
        Assert.True(_proc.LogicalOutputState.D);

        // Release D
        var acts3 = _proc.ProcessKey(GameKey.D, false);
        Assert.Single(acts3);
        Assert.Equal(new KeyAction(GameKey.D, false), acts3[0]);
        Assert.False(_proc.LogicalOutputState.D);
    }

    // ────────────────────────────────────────────────────────
    // 4. Ctrl 優先與互斥 (Ctrl Priority / Mutual Exclusion)
    // ────────────────────────────────────────────────────────

    [Fact]
    public void CtrlPriority_PressW_ThenPressCtrl_StopsW_ActivatesCtrl()
    {
        _proc.Mode = SocdMode.Neutral;
        _proc.EnableCtrlPriority = true;

        // Press W
        _proc.ProcessKey(GameKey.W, true);
        Assert.True(_proc.LogicalOutputState.W);

        // Press Ctrl -> W should be released, Ctrl should be pressed
        var acts = _proc.ProcessKey(GameKey.Ctrl, true);
        Assert.Contains(new KeyAction(GameKey.W, false), acts);
        Assert.Contains(new KeyAction(GameKey.Ctrl, true), acts);
        Assert.False(_proc.LogicalOutputState.W);
        Assert.True(_proc.LogicalOutputState.Ctrl);

        // While Ctrl held, press A -> Ctrl should be released, A should activate!
        var acts2 = _proc.ProcessKey(GameKey.A, true);
        Assert.Contains(new KeyAction(GameKey.Ctrl, false), acts2);
        Assert.Contains(new KeyAction(GameKey.A, true), acts2);
        Assert.False(_proc.LogicalOutputState.Ctrl);
        Assert.True(_proc.LogicalOutputState.A);

        // Release W and A (while Ctrl is still held) -> Ctrl resumes!
        _proc.ProcessKey(GameKey.W, false);
        var acts3 = _proc.ProcessKey(GameKey.A, false);
        Assert.Contains(new KeyAction(GameKey.A, false), acts3);
        Assert.Contains(new KeyAction(GameKey.Ctrl, true), acts3);
        Assert.False(_proc.LogicalOutputState.A);
        Assert.True(_proc.LogicalOutputState.Ctrl);

        // Release Ctrl
        _proc.ProcessKey(GameKey.Ctrl, false);
        Assert.False(_proc.LogicalOutputState.Ctrl);
    }

    [Fact]
    public void CtrlPriority_Disabled_CtrlDoesNotAffectWASD()
    {
        _proc.Mode = SocdMode.Neutral;
        _proc.EnableCtrlPriority = false;

        _proc.ProcessKey(GameKey.W, true);
        _proc.ProcessKey(GameKey.Ctrl, true);

        // Both W and Ctrl are active
        Assert.True(_proc.LogicalOutputState.W);
        Assert.True(_proc.LogicalOutputState.Ctrl);
    }
}
