using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using System.Windows.Forms;
using SOCD.App.Interop;
using SOCD.App.ViewModels;
using System.Diagnostics;
using System.Drawing; // 需確保有引用 System.Drawing
using System.Runtime.InteropServices;

namespace SOCD.App;

public partial class MainWindow : Window
{

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    // 改成可空型別 (Nullable) 就不會有警告了
    private System.Windows.Forms.NotifyIcon? _notifyIcon;

    private readonly MainViewModel _vm = new();
    private readonly LowLevelKeyboardHook _hook;

    // 顏色定義
    private static readonly SolidColorBrush PhysOffBg   = new(Color.FromRgb(0x28, 0x30, 0x48));
    private static readonly SolidColorBrush PhysOnBg    = new(Color.FromRgb(0x00, 0xb4, 0xd8));
    private static readonly SolidColorBrush PhysOnFg    = new(Color.FromRgb(0x12, 0x14, 0x1d));
    private static readonly SolidColorBrush PhysOffFg   = new(Color.FromRgb(0x88, 0x92, 0xb0));

    private static readonly SolidColorBrush OutOffBg    = new(Color.FromRgb(0x28, 0x30, 0x48));
    private static readonly SolidColorBrush OutOnBg     = new(Color.FromRgb(0xff, 0x00, 0x55));
    private static readonly SolidColorBrush OutOnFg     = new(Colors.White);
    private static readonly SolidColorBrush OutOffFg    = new(Color.FromRgb(0x66, 0x70, 0x85));

    private static readonly SolidColorBrush NeutralClr  = new(Color.FromRgb(0xff, 0xb7, 0x03));
    private static readonly SolidColorBrush ActiveClr   = new(Color.FromRgb(0x00, 0xe5, 0xff));
    private static readonly SolidColorBrush BorderOff   = new(Color.FromRgb(0x3a, 0x45, 0x66));
    private static readonly SolidColorBrush BorderOn    = new(Color.FromRgb(0x00, 0xe5, 0xff));

    public MainWindow()
    {
        InitializeComponent();
        // 設定系統列（右下角）小圖示
        InitTrayIcon();
        // 當視窗內容完全渲染完成後執行清理
        this.ContentRendered += (s, e) => FlushMemory();
        DataContext = _vm;

        // 初始化鍵盤鉤子並與 SocdProcessor 綁定
        _hook = new LowLevelKeyboardHook(_vm.Processor);
        _hook.StateChanged += () =>
        {
            Dispatcher.Invoke(() =>
            {
                _vm.RefreshAll();
                RefreshVisuals();
            });
        };

        // 綁定 ViewModel Hook 開關事件
        _vm.HookToggled += enabled =>
        {
            if (enabled)
            {
                _hook.Install();
                StatusText.Text = "✅ 攔截中 — 請記得在SOCD模式設定完成後將此程式視窗最小化，已取得最佳效能。若要再次調整設定或關閉此程式，可以在工作列右下角點擊^圖示，雙擊點開此應用程式。";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xe5, 0xff));
            }
            else
            {
                _hook.Uninstall();
                StatusText.Text = "⏸️ 攔截已暫停 — 鍵盤輸入已恢復原生狀態";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xb7, 0x03));
            }
            RefreshVisuals();
        };

        _vm.PropertyChanged += (_, _) => Dispatcher.Invoke(RefreshVisuals);

        // 預設啟動 Hook
        _hook.Install();
        RefreshVisuals();
    }

    private void InitTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application, // 使用預設圖示，也可以換成自己的 .ico
                Visible = true,
                Text = "SOCD 處理器 (運作中)"
            };

            // 點擊右下角圖示重新顯示視窗
            _notifyIcon.Click += (s, e) =>
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            };
        }

        // 攔截視窗最小化事件：不最小化，而是直接隱藏視窗（Hide）
        protected override void OnStateChanged(EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.Hide(); // 隱藏 WPF UI 視窗
                FlushMemory(); // 強制釋放記憶體至 3MB~8MB
            }
            base.OnStateChanged(e);
        }

        // 當程式徹底關閉時釋放圖示
        protected override void OnClosed(EventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnClosed(e);
        }

        public static void FlushMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            }
            catch { }
        }

    private void RefreshVisuals()
    {
        // 1. 實體按鍵視覺更新
        SetKeyStyle(PhysKeyW, _vm.PhysW, PhysOnBg, PhysOffBg, PhysOnFg, PhysOffFg);
        SetKeyStyle(PhysKeyA, _vm.PhysA, PhysOnBg, PhysOffBg, PhysOnFg, PhysOffFg);
        SetKeyStyle(PhysKeyS, _vm.PhysS, PhysOnBg, PhysOffBg, PhysOnFg, PhysOffFg);
        SetKeyStyle(PhysKeyD, _vm.PhysD, PhysOnBg, PhysOffBg, PhysOnFg, PhysOffFg);
        SetKeyStyle(PhysKeyCtrl, _vm.PhysCtrl, PhysOnBg, PhysOffBg, PhysOnFg, PhysOffFg);

        // 2. 輸出方向視覺更新
        SetKeyStyle(OutKeyW, _vm.OutW, OutOnBg, OutOffBg, OutOnFg, OutOffFg);
        SetKeyStyle(OutKeyA, _vm.OutA, OutOnBg, OutOffBg, OutOnFg, OutOffFg);
        SetKeyStyle(OutKeyS, _vm.OutS, OutOnBg, OutOffBg, OutOnFg, OutOffFg);
        SetKeyStyle(OutKeyD, _vm.OutD, OutOnBg, OutOffBg, OutOnFg, OutOffFg);
        SetKeyStyle(OutKeyCtrl, _vm.OutCtrl, OutOnBg, OutOffBg, OutOnFg, OutOffFg);

        // 3. 中立中心點
        if (_vm.OutNone)
        {
            OutCenter.Background = NeutralClr;
            OutCenter.BorderBrush = NeutralClr;
        }
        else
        {
            OutCenter.Background = OutOffBg;
            OutCenter.BorderBrush = BorderOff;
        }

        // 4. 方向文字
        DirectionText.Text = _vm.DirectionLabel;
        DirectionText.Foreground = _vm.OutNone ? NeutralClr : ActiveClr;
    }

    private static void SetKeyStyle(System.Windows.Controls.Border border, bool active,
        SolidColorBrush onBg, SolidColorBrush offBg,
        SolidColorBrush onFg, SolidColorBrush offFg)
    {
        border.Background = active ? onBg : offBg;
        border.BorderBrush = active ? BorderOn : BorderOff;
        if (border.Child is System.Windows.Controls.TextBlock tb)
        {
            tb.Foreground = active ? onFg : offFg;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _hook.Dispose();
    }
}