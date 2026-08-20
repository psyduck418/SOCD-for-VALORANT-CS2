using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SOCD.Core;

namespace SOCD.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly SocdProcessor _processor = new();

    public SocdProcessor Processor => _processor;

    public MainViewModel()
    {
        _processor.Mode = SocdMode.Neutral;
        _processor.EnableCtrlPriority = true; // 預設開啟
    }

    // ────────────────────────────────────────────────────────
    // SOCD 模式切換
    // ────────────────────────────────────────────────────────

    public bool IsNeutral
    {
        get => _processor.Mode == SocdMode.Neutral;
        set
        {
            if (value && _processor.Mode != SocdMode.Neutral)
            {
                _processor.Mode = SocdMode.Neutral;
                OnPropertyChanged();
                RefreshAll();
                AddLog("⚙ 模式切換", "中立模式 (Neutral)");
            }
        }
    }

    public bool IsLastInput
    {
        get => _processor.Mode == SocdMode.LastInputPriority;
        set
        {
            if (value && _processor.Mode != SocdMode.LastInputPriority)
            {
                _processor.Mode = SocdMode.LastInputPriority;
                OnPropertyChanged();
                RefreshAll();
                AddLog("⚙ 模式切換", "後按優先 (Last Input Priority)");
            }
        }
    }

    public bool IsFirstInput
    {
        get => _processor.Mode == SocdMode.FirstInputPriority;
        set
        {
            if (value && _processor.Mode != SocdMode.FirstInputPriority)
            {
                _processor.Mode = SocdMode.FirstInputPriority;
                OnPropertyChanged();
                RefreshAll();
                AddLog("⚙ 模式切換", "先按優先 (First Input Priority)");
            }
        }
    }

    // ────────────────────────────────────────────────────────
    // Ctrl 優先功能開關
    // ────────────────────────────────────────────────────────

    public bool EnableCtrlPriority
    {
        get => _processor.EnableCtrlPriority;
        set
        {
            if (_processor.EnableCtrlPriority != value)
            {
                _processor.EnableCtrlPriority = value;
                OnPropertyChanged();
                RefreshAll();
                AddLog("🎮 Ctrl 優先", value ? "已啟用 (按住Ctrl停止WASD / 按WASD停用Ctrl)" : "已停用");
            }
        }
    }

    // ────────────────────────────────────────────────────────
    // 全域 Hook 開關
    // ────────────────────────────────────────────────────────

    private bool _hookEnabled = true; // 預設開啟全域攔截
    public bool HookEnabled
    {
        get => _hookEnabled;
        set
        {
            if (_hookEnabled != value)
            {
                _hookEnabled = value;
                OnPropertyChanged();
                HookToggled?.Invoke(value);
                AddLog("🌐 攔截狀態", value ? "全域 SOCD 攔截已啟動" : "全域 SOCD 攔截已停止");
            }
        }
    }

    public event Action<bool>? HookToggled;

    // ────────────────────────────────────────────────────────
    // 實體按鍵狀態 (Physical)
    // ────────────────────────────────────────────────────────

    public bool PhysW => _processor.PhysicalState.W;
    public bool PhysA => _processor.PhysicalState.A;
    public bool PhysS => _processor.PhysicalState.S;
    public bool PhysD => _processor.PhysicalState.D;
    public bool PhysCtrl => _processor.PhysicalState.Ctrl;

    // ────────────────────────────────────────────────────────
    // 邏輯輸出狀態 (Logical Cleaned Output)
    // ────────────────────────────────────────────────────────

    public bool OutW => _processor.LogicalOutputState.W;
    public bool OutA => _processor.LogicalOutputState.A;
    public bool OutS => _processor.LogicalOutputState.S;
    public bool OutD => _processor.LogicalOutputState.D;
    public bool OutCtrl => _processor.LogicalOutputState.Ctrl;

    public bool OutNone => !OutW && !OutA && !OutS && !OutD;

    public string DirectionLabel
    {
        get
        {
            var dir = _processor.CleanedDirection;
            if (dir == Direction.None)
            {
                return OutCtrl ? "[CTRL]" : "NEUTRAL";
            }
            return OutCtrl ? $"{dir} + CTRL" : dir.ToString().ToUpperInvariant();
        }
    }

    // ────────────────────────────────────────────────────────
    // 日誌記錄
    // ────────────────────────────────────────────────────────

    public ObservableCollection<string> LogEntries { get; } = new();

    public void AddLog(string title, string details)
    {
        var time = DateTime.Now.ToString("HH:mm:ss.fff");
        LogEntries.Insert(0, $"[{time}] {title,-10} {details}");
        if (LogEntries.Count > 100)
        {
            LogEntries.RemoveAt(LogEntries.Count - 1);
        }
    }

    public void RefreshAll()
    {
        OnPropertyChanged(nameof(PhysW));
        OnPropertyChanged(nameof(PhysA));
        OnPropertyChanged(nameof(PhysS));
        OnPropertyChanged(nameof(PhysD));
        OnPropertyChanged(nameof(PhysCtrl));

        OnPropertyChanged(nameof(OutW));
        OnPropertyChanged(nameof(OutA));
        OnPropertyChanged(nameof(OutS));
        OnPropertyChanged(nameof(OutD));
        OnPropertyChanged(nameof(OutCtrl));
        OnPropertyChanged(nameof(OutNone));
        OnPropertyChanged(nameof(DirectionLabel));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
