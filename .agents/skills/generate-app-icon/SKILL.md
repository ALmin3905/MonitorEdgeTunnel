---
name: generate-app-icon
description: 產生或更新 MonitorEdgeTunnel 的應用程式、捷徑與通知區圖示，從 SVG 來源輸出多尺寸 ICO 並檢查清晰度及透明度；不適用於視窗內的 XAML 按鈕圖示。
---

# 產生應用程式圖示

維護 SVG 向量來源，讓每個 ICO 尺寸直接從向量繪製。Windows 程式、捷徑與通知區使用多尺寸 ICO；不能把專案的 `ApplicationIcon` 直接改成 SVG。

## 來源與整合位置

- 向量來源：[monitor.svg](../../../MonitorEdgeTunnelApp/Resources/monitor.svg)。僅提升畫質時保留原圖樣與配色；有重新設計需求時依使用者要求調整。
- 正式圖示：[monitor.ico](../../../MonitorEdgeTunnelApp/Resources/monitor.ico)。維持這個資源路徑，讓現有使用位置取得同一份圖示。
- 產生器：[Generate-ApplicationIcon.ps1](../../../tools/Generate-ApplicationIcon.ps1)。重用此腳本，不另寫一套 ICO 編碼器。
- [MonitorEdgeTunnelApp.csproj](../../../MonitorEdgeTunnelApp/MonitorEdgeTunnelApp.csproj) 以 `ApplicationIcon` 嵌入 ICO，並將 ICO 宣告為 WPF `Resource`；SVG 以 `None` 保留作為來源。
- [App.xaml.cs](../../../MonitorEdgeTunnelApp/App.xaml.cs) 從該 WPF 資源建立通知區圖示，以 `SystemInformation.SmallIconSize` 選取影格。捷徑由 EXE 取得圖示。

這個流程處理應用程式圖示；不需改動主視窗隱藏標題列圖示的設計。

## 編輯向量來源

先確認目前相關差異與圖示實際用法，再依需求修改 SVG。不要以放大舊的低解析圖片代替向量來源。

目前產生器只支援以下 SVG 子集：

- 正方形 `viewBox="0 0 S S"`。
- SVG 根元素下的一個直接子 `path`，`fill="none"`；同一 Path 可用多個子路徑。
- 在 Path 上明確提供 `stroke`、`stroke-width`、`stroke-linecap`、`stroke-linejoin`。
- 路徑資料須能由 WPF `Geometry.Parse` 解析。

它不是通用 SVG renderer。若設計需要填色、多色、多個 Path、圖片或濾鏡，先擴充產生器或選擇合適的向量繪製工具，並驗證輸出；不要直接套用目前腳本。

## 產生與預覽

在包含 `MonitorEdgeTunnel.sln` 的專案根目錄執行。使用 Windows PowerShell 的 STA 執行緒與 Windows WPF，不需要額外圖像套件。

更新正式 ICO：

```powershell
powershell.exe -NoProfile -STA -File .\tools\Generate-ApplicationIcon.ps1
```

僅預覽或驗證時，將輸出放到新的暫存目錄，保留正式 ICO：

```powershell
$iconPreviewDirectory = Join-Path ([IO.Path]::GetTempPath()) ('MonitorEdgeTunnel-icon-' + [Guid]::NewGuid().ToString('N'))
powershell.exe -NoProfile -STA -File .\tools\Generate-ApplicationIcon.ps1 -OutputPath (Join-Path $iconPreviewDirectory 'monitor.ico') -PreviewDirectory $iconPreviewDirectory
```

`-SourcePath` 可指定其他符合上述子集的 SVG。`-PreviewDirectory` 會建立目錄並輸出每個尺寸的 `monitor-N.png`；正式來源與 ICO 的預設路徑相對於腳本位置，不依賴啟動時的工作目錄。

尺寸清單以腳本的 `$sizes` 為準；目前包含 15 種尺寸，涵蓋 16～256px。小尺寸使用 32-bit BGRA DIB 與透明遮罩，256px 使用 PNG。各尺寸分別繪製，不從單一點陣影格放大。

## 驗證與回報

- 讀取 ICO 目錄，確認影格數量、尺寸與產生器一致；目錄中的寬高位元組 `0` 代表 256px。各影格應可解碼並保留透明度。
- 使用 WPF `BitmapDecoder` 確認完整影格集合，包括 256px PNG；通知區小尺寸另可用 `System.Drawing.Icon(stream, size, size)` 確認選取結果。不要以該 GDI+ 建構式是否選到 256px 判定大尺寸 PNG 是否存在。
- 將預覽合成到深淺色背景，以實際 16、20、24、32px 尺寸確認辨識度、邊緣與透明區域；同時檢查大尺寸曲線，不能只看放大圖。
- 修改產生器時，重複產生至暫存路徑並比較正式 ICO 的雜湊，確認可重現性。
- 需要驗證程式整合時，依 [run-tests](../run-tests/SKILL.md) 使用暫存副本建置 Debug/Release x64；確認新增檔案確實包含在副本中。要宣稱嵌入完整，須檢查編譯 EXE 的 `RT_GROUP_ICON`／`RT_ICON`，比對影格尺寸及內容。

回報 SVG、ICO 與產生器路徑，實際輸出的尺寸，以及執行過的驗證與命令。若只產圖或預覽，明確區分未執行的建置與 EXE 資源檢查。
