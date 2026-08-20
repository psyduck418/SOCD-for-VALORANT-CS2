using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SOCD.Core;

namespace SOCD.App.Interop;

/// <summary>
/// Win32 低階鍵盤鉤子 (WH_KEYBOARD_LL) 與 SendInput 注入器。
/// 攔截實體 WASD 與 Ctrl 按鍵，依據 SOCD 規則進行過濾與合成注入，真正達到吞鍵 (Suppress) 與取代效果。
/// </summary>
public sealed class LowLevelKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    // 自訂 ExtraInfo 標記 (0x534F4344 = ASCII "SOCD")，用於識別由本程式合成發送的按鍵，防止自身遞迴攔截
    public static readonly UIntPtr SOCD_MAGIC_INFO = new(0x534F4344);

    private readonly SocdProcessor _processor;
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;

    public event Action? StateChanged;

    public LowLevelKeyboardHook(SocdProcessor processor)
    {
        _processor = processor;
        // 保持委派的參考，防止被 GC 回收
        _proc = HookCallback;
    }

    public bool IsInstalled => _hookId != IntPtr.Zero;

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;

            // 釋放任何殘留的輸出狀態，避免按鍵卡住
            var releaseActions = _processor.Reset();
            SendSyntheticActions(releaseActions);
            StateChanged?.Invoke();
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            // 1. 若為本程式 SendInput 發出的合成事件，直接放行給目標視窗/系統
            if (hookStruct.dwExtraInfo == SOCD_MAGIC_INFO)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            int vkCode = (int)hookStruct.vkCode;
            int msg = (int)wParam;
            bool isDown = (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN);
            bool isUp = (msg == WM_KEYUP || msg == WM_SYSKEYUP);

            if (isDown || isUp)
            {
                GameKey? gameKey = MapVkToGameKey(vkCode);
                if (gameKey != null)
                {
                    // 2. 透過 SOCD 引擎計算狀態變更與需要執行的合成動作
                    var actions = _processor.ProcessKey(gameKey.Value, isDown);

                    // 3. 執行合成按鍵動作 (SendInput)
                    if (actions.Count > 0)
                    {
                        SendSyntheticActions(actions);
                    }
                    else if (isDown)
                    {
                        // 處理長按鍵盤重複 (Key Repeat): 若該鍵在 SOCD 判定下當前屬於「有效輸出」，則發送重複按鍵
                        if (IsKeyLogicallyActive(gameKey.Value))
                        {
                            SendSingleKey(gameKey.Value, true);
                        }
                    }

                    // 4. 通知 UI 更新視覺狀態
                    StateChanged?.Invoke();

                    // 5. 吞掉原始實體按鍵事件，徹底阻斷作業系統接收未清洗的輸入！
                    return (IntPtr)1;
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private bool IsKeyLogicallyActive(GameKey key)
    {
        var outState = _processor.LogicalOutputState;
        return key switch
        {
            GameKey.W => outState.W,
            GameKey.A => outState.A,
            GameKey.S => outState.S,
            GameKey.D => outState.D,
            GameKey.Ctrl => outState.Ctrl,
            _ => false
        };
    }

    public static void SendSyntheticActions(List<KeyAction> actions)
    {
        if (actions == null || actions.Count == 0) return;

        var inputs = new INPUT[actions.Count];
        for (int i = 0; i < actions.Count; i++)
        {
            var act = actions[i];
            ushort vk = (ushort)(act.Key == GameKey.Ctrl ? 0x11 /* VK_CONTROL */ : (int)act.Key);
            uint flags = act.IsDown ? 0u : 0x0002u; // 0 = KEYEVENTF_KEYDOWN, 2 = KEYEVENTF_KEYUP

            inputs[i] = new INPUT
            {
                type = 1, // INPUT_KEYBOARD
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = (ushort)MapVirtualKey(vk, 0),
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = SOCD_MAGIC_INFO
                    }
                }
            };
        }

        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != (uint)inputs.Length)
        {
            Debug.WriteLine($"[SOCD] SendInput failed: sent {sent}/{inputs.Length}, error: {Marshal.GetLastWin32Error()}");
        }
    }

    private static void SendSingleKey(GameKey key, bool isDown)
    {
        ushort vk = (ushort)(key == GameKey.Ctrl ? 0x11 : (int)key);
        var input = new INPUT
        {
            type = 1,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = (ushort)MapVirtualKey(vk, 0),
                    dwFlags = isDown ? 0u : 0x0002u,
                    time = 0,
                    dwExtraInfo = SOCD_MAGIC_INFO
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    public static GameKey? MapVkToGameKey(int vkCode)
    {
        return vkCode switch
        {
            0x57 => GameKey.W,
            0x41 => GameKey.A,
            0x53 => GameKey.S,
            0x44 => GameKey.D,
            0x11 or 0xA2 or 0xA3 => GameKey.Ctrl, // VK_CONTROL, VK_LCONTROL, VK_RCONTROL
            _ => null
        };
    }

    public void Dispose()
    {
        Uninstall();
    }

    // ────────────────────────────────────────────────────────
    // Win32 P/Invoke 結構與宣告 (精確對齊 64 位元結構，大小 40 位元組)
    // ────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
}
