#include <Windows.h>
#include <iostream>
#include "MonitorEdgeTunnelManager.h"

bool SysKeydownCallback_Z(DWORD keycode);
void CmdLoop();

// 顯示錯誤原因並退出
#define FailedExit(msg) \
    std::cout << msg << std::endl; \
    if (hMutex) \
        ::CloseHandle(hMutex); \
    system("pause"); \
    return 1; \

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

    // 啟動
    MonitorEdgeTunnelManager& monitorEdgeTunnelManager = MonitorEdgeTunnelManager::GetInstance();
    monitorEdgeTunnelManager.SetKeycodeCallback('Z', SysKeydownCallback_Z);
    monitorEdgeTunnelManager.Start();

    // 顯示console
    ::ShowWindow(::GetConsoleWindow(), SW_SHOW);

    // 使用者輸入指令
    CmdLoop();

    // 停止
    monitorEdgeTunnelManager.Stop();

    // 釋放系統互斥鎖
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

void CmdLoop()
{
    MonitorEdgeTunnelManager& monitorEdgeTunnelManager = MonitorEdgeTunnelManager::GetInstance();

    std::string cmd;
    while (true)
    {
        cmd.clear();

        std::cout << "請輸入指令 : ";
        std::cin >> cmd;

        if (cmd[0] == 'q')
            break;
        else if (cmd[0] == 'z')
            ::ShowWindow(::GetConsoleWindow(), SW_HIDE);
        else if (cmd[0] == 'r')
            monitorEdgeTunnelManager.Start();
        else if (cmd[0] == 't')
            monitorEdgeTunnelManager.Stop();
    }
}