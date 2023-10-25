#include <Windows.h>
#include <iostream>
#include "HookManager.h"
#include "MonitorInfoManager.h"
#include "MouseEdgeManager.h"
#include "SettingManager.h"

bool SysKeydownCallback_Z(DWORD keycode);

#define FailedExit(msg) \
    std::cout << msg << std::endl; \
    if (hMutex) \
        ::CloseHandle(hMutex); \
    system("pause"); \
    return -1; \

int main()
{
    // 使用系統互斥鎖避免多重啟動程式
    HANDLE hMutex = ::CreateMutex(NULL, FALSE, L"_MonitorEdgeTunnelMutex_");
    if (!hMutex)
    {
        FailedExit("Failed to create mutex")
    }
    if (::GetLastError() == ERROR_ALREADY_EXISTS)
    {
        FailedExit("Application already exists");
    }

    // ---------- some instance ---------- //
    HookManager& hookManager = HookManager::GetInstance();
    MouseEdgeManager& mouseEdgeManager = MouseEdgeManager::GetInstance();
    SettingManager& settingManager = SettingManager::GetInstance();

    // ---------- init ---------- //
    {
        // 取得螢幕資訊
        MonitorInfoList monitorInfoList;
        if (!MonitorInfoManager::GetMonitorInfoList(monitorInfoList))
        {
            FailedExit("Failed to get monitor info")
        }
        if (monitorInfoList.empty())
        {
            FailedExit("No monitor Info")
        }

        // 嘗試取得設定，失敗就建立一個新的
        try
        {
            settingManager.Load();
        }
        catch (const std::exception& e)
        {
            std::cout << e.what() << std::endl;
            settingManager.Save();
        }

        // 添加TunnelInfoList至MonitorInfoList
        if (!MonitorInfoManager::AppendTunnelInfoToMonitorInfo(monitorInfoList, settingManager.TunnelInfoList))
        {
            FailedExit("Failed to append tunnel info")
        }

        // 初始化螢幕資訊
        try
        {
            mouseEdgeManager.UpdateMonitorInfo(monitorInfoList, settingManager.ForceForbidEdge);
        }
        catch (const std::exception& e)
        {
            FailedExit(e.what())
        }
    }

    // 設定hook callback
    hookManager.SetKeycodeCallback('Z', SysKeydownCallback_Z);
    hookManager.SetMouseMoveCallback([&mouseEdgeManager](POINT& pt) { return mouseEdgeManager.EdgeTunnelTransport(pt); });

    // hook start
    hookManager.Start();

    ::ShowWindow(::GetConsoleWindow(), SW_SHOW);

    // ---------- get cmd ---------- //
    char cmd[10] = { 0 };
    while (true)
    {
        std::cout << "請輸入指令 : ";
        std::cin >> cmd;

        if (cmd[0] == 'q')
            break;
        else if (cmd[0] == 'z')
            ::ShowWindow(::GetConsoleWindow(), SW_HIDE);
    }

    // ---------- deinit ---------- //
    hookManager.Stop();

    if (hMutex)
        ::CloseHandle(hMutex);

    return 0;
}

bool SysKeydownCallback_Z(DWORD keycode)
{
    if (!::IsWindowVisible(::GetConsoleWindow()))
    {
        ::ShowWindow(::GetConsoleWindow(), SW_SHOW);

        // 微軟會阻止非focus的執行緒使用SetFocus之類的功能，所以必須將輸入訊息交給focus中的執行緒處理
        DWORD dwCurrentThread = GetCurrentThreadId();                               // 當前程式執行緒
        DWORD dwFGThread = GetWindowThreadProcessId(GetForegroundWindow(), NULL);   // 有focus狀態的執行緒
        AttachThreadInput(dwCurrentThread, dwFGThread, TRUE);                       // 附加輸入訊息到其他執行緒
        BringWindowToTop(::GetConsoleWindow());                                     // console移到最前面
        SetFocus(::GetConsoleWindow());                                             // focus console
        AttachThreadInput(dwCurrentThread, dwFGThread, FALSE);                      // 解除附加

        return true;
    }

    return false;
}