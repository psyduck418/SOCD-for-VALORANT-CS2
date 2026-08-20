# SOCD Cleaner & Processor (Windows / WPF)

高效、低延遲的 Windows 系統級 **SOCD（Simultaneous Opposite Cardinal Direction，同時相反方向輸入）清除與處理器**。

基於 **.NET 9 + WPF** 與 Win32 低階鍵盤鉤子 (`WH_KEYBOARD_LL`) + `SendInput` 打造，支援在各類遊戲（格鬥遊戲、FPS、跑跑卡丁車等）及應用程式中即時攔截並過濾相反方向按鍵。

---

## ✨ 核心特色

### 1. 三種 SOCD 清除模式 (Cleaning Modes)
- **中立模式 (Neutral)**：
  - 同時按下相反方向時（如 A+D 或 W+S）相互抵銷為中立狀態（不輸出）。
  - 放開其中一鍵時自動恢復另一鍵。
- **後按優先 (Last Input Priority / 2IP)**：
  - 同時按下相反方向時，以最新按下的鍵優先輸出（例如先按住 A 再按下 D，輸出為 D）。
  - 當放開新鍵時，若舊鍵仍按著則自動無縫回退輸出舊鍵。
- **先按優先 (First Input Priority / Absolute Priority)**：
  - 按住第一鍵時，第二鍵的按下事件被完全吞除（Block）。
  - 當放開第一鍵時，若第二鍵仍按著則自動接續輸出第二鍵。

### 2. Ctrl 優先與互斥功能 (獨立開關)
- **移動中按 Ctrl**：立即釋放 WASD 方向指令，強制進入靜止/蹲下狀態。
- **按住 Ctrl 時按 WASD**：立即停用 Ctrl，自動恢復 WASD 移動。
- **放開移動鍵**：若 Ctrl 仍按著，自動恢復輸出 Ctrl。

### 3. 系統級真實攔截 (Kernel Suppression)
- 透過 Win32 底層鉤子回傳 `(IntPtr)1` 徹底吞除被覆蓋或抵銷的實體按鍵，防止遊戲接收到未清洗的原始輸入。
- 透過 `SendInput` 注入合成按鍵（附帶自訂 `dwExtraInfo = 0x534F4344` 防止遞迴攔截）。
- 內建 `app.manifest` 強制要求系統管理員權限，確保在各類以管理員權限運行的遊戲中皆能穩定生效。

### 4. 視覺化即時面板 (Modern Dark UI)
- 即時顯示實體按鍵（Physical KeyDown）與 SOCD 清洗後送給系統的輸出（Cleaned Output）。
- 包含即時決策歷史日誌，方便除錯與監控。

---

## 🏗️ 專案架構

```
SOCD/
├── SOCD.sln                       # .NET 解決方案
├── .gitignore                     # Git 忽略設定
├── README.md                      # 專案說明文件
├── SOCD.Core/                     # 核心演算法庫（純 C#，無 UI 依賴）
│   ├── Enums.cs                   # SocdMode, GameKey, Direction, KeyAction
│   ├── DirectionState.cs          # 狀態快照資料結構
│   └── SocdProcessor.cs          # SOCD 狀態機與決策引擎
├── SOCD.Tests/                    # 單元測試專案 (xUnit)
│   ├── SocdProcessorTests.cs     # 核心模式測試（Neutral, LastInput, FirstInput, Ctrl）
│   └── Win32StructTests.cs       # 64 位元 Win32 記憶體結構對齊測試
└── SOCD.App/                      # WPF 前端應用程式
    ├── app.manifest               # 系統管理員權限設定 (requireAdministrator)
    ├── App.xaml / App.xaml.cs
    ├── MainWindow.xaml            # 深色現代化介面
    ├── MainWindow.xaml.cs
    ├── ViewModels/
    │   └── MainViewModel.cs       # MVVM 視圖模型
    └── Interop/
        └── LowLevelKeyboardHook.cs # Win32 鍵盤鉤子與 SendInput 注入器
```

---

## 🔨 建置與執行

### 開發環境需求
- Windows 10 / 11 (64-bit)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### 編譯專案
```bash
dotnet build -c Release
```

### 執行單元測試
```bash
dotnet test -c Release
```

### 打包發布獨立單一執行檔 (.exe)
```bash
dotnet publish SOCD.App/SOCD.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```
發布產物將產生於 `publish/SOCD_Processor.exe`，內建 .NET 執行環境，免安裝即可直接執行。

---

## 📄 授權條款
MIT License
