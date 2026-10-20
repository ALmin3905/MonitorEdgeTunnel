---
name: run-tests
description: 執行或說明 MonitorEdgeTunnel 專案測試時使用，選擇函式庫、C# 回歸測試、單組測試或暫存副本的 Debug/Release x64 建置驗證。
---

# MonitorEdgeTunnel 測試

以 [tests/run_tests.py](../../../tests/run_tests.py) 為測試入口及參數的依據。使用者詢問執行方法時提供命令；要求執行時依指定範圍操作。

以下命令均在同時包含 `MonitorEdgeTunnel.sln` 與 `tests/run_tests.py` 的專案根目錄執行。

## 執行環境

- 使用 Windows 與 Python 3；一般 PowerShell 即可。若 `python` 不可用而已安裝 Python Launcher，可將命令開頭換成 `py -3`。
- 安裝 Visual Studio C++ 工具與 Windows SDK。腳本會讀取 `MonitorEdgeTunnelLib.vcxproj` 的 `PlatformToolset`，自動選取對應的 Visual Studio、MSVC、INCLUDE、LIB 與 PATH；目前專案使用 `v145`。
- C# 測試與完整 solution 建置需要 .NET Framework 4.7.2 開發／目標套件。`interop` 另需已還原的 `packages/Serilog.4.3.0/lib/net471/Serilog.dll`；`solution` 需要專案的完整 `packages` 目錄及 Git。
- 缺少依賴時，依錯誤指出所缺的工具或套件；測試執行器本身使用 Python 標準函式庫。

## 選擇並執行

先依需求選擇範圍；可用 `--list` 確認目前有哪些測試組。

| 需求 | PowerShell 命令 |
|---|---|
| 列出測試名稱 | `python tests/run_tests.py --list` |
| MonitorEdgeTunnelLib 全部原生測試 | `python tests/run_tests.py lib` |
| 全部原生與 C# 測試，含原生編譯檢查 | `python tests/run_tests.py` |
| 只執行通道映射測試組 | `python tests/run_tests.py mouse_mapping_tests` |
| 一次執行多個測試組 | `python tests/run_tests.py mouse_mapping_tests settings_extended_tests` |
| C# 回歸測試 | `python tests/run_tests.py interop pipe save_ui` |
| 只編譯原生 Lib 與 DLL 的各個來源檔 | `python tests/run_tests.py compile` |
| 完整 Debug/Release x64 建置及 EXE/DLL 架構檢查 | `python tests/run_tests.py solution` |

測試名稱是 `tests/native/*.cpp` 或 `tests/managed/*.cs` 去掉副檔名後的檔名。指定名稱會執行整個測試組。

`solution` 僅進行完整建置驗證。需要全部測試及完整建置時，依序執行：

```powershell
python tests/run_tests.py
python tests/run_tests.py solution
```

## 隔離與驗證範圍

- 原生及 C# 測試會在暫存目錄編譯並執行，離開執行器時清除產物；以此入口維持工作目錄的建置輸出不受影響。
- `solution` 透過 `git ls-files` 複製已追蹤檔案的目前內容，另外複製 `packages`，再於該暫存副本建置。未追蹤的新檔不會包含在副本中；解讀建置結果時須確認要驗證的檔案已納入。
- 原生測試透過模擬 hook、游標及螢幕探索 API 驗證相關行為；隱藏視窗的部分測試使用實際 Windows 訊息迴圈。自動測試結果與實際多螢幕、混合 DPI、全域 hook 操作的實機驗證分開回報。
- 執行器會先檢查專案及測試的 C# 原始碼是否為 UTF-8 BOM、CRLF，並檢查 tab 縮排；此檢查也適用於 `lib` 與 `--list`。

## 判讀及回報

- 各組通過會印出 `PASS 測試名稱`；所有指定組別完成後，最後印出 `Passed N suites in ...s` 並以退出碼 0 結束。
- 錯誤路徑測試會刻意印出 `Log [Error]`，以程序退出碼、各組結果及最後總結判斷成敗。
- 編譯失敗、斷言失敗或逾時會以非零退出碼中止後續組別。保留失敗組名、第一個有用的錯誤及可重現的執行命令。
- 回報實際執行的命令、工具鏈及結果；略過的項目與實機驗證限制應明確說明。
