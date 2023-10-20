#include <Windows.h>
#include <iostream>
#include "HookManager.h"
#include "MonitorInfoManager.h"
#include "MouseEdgeManager.h"

bool SysKeydownCallback_Z(DWORD keycode);

int main()
{
    // ---------- some instance ---------- //
    HookManager& hookManager = HookManager::GetInstance();
    MouseEdgeManager& mouseEdgeManager = MouseEdgeManager::GetInstance();

    // ---------- init ---------- //
    {
        // 取得螢幕資訊
        std::vector<std::shared_ptr<MonitorInfo>> monitorInfoList;
        if (!MonitorInfoManager::GetMonitorInfoList(monitorInfoList))
        {
            std::cout << "Failed to get monitor info" << std::endl;
            system("pause");
            return -1;
        }
        if (monitorInfoList.empty())
        {
            std::cout << "No monitor Info" << std::endl;
            system("pause");
            return -1;
        }

        // TODO : 暫時先將tunnel規則寫死在程式裡
        {
            TunnelInfo tunnelInfo = { 0 };
            tunnelInfo.id = 0;
            tunnelInfo.from = monitorInfoList[0]->top;
            tunnelInfo.to = monitorInfoList[0]->bottom;
            tunnelInfo.relativeID = 1;
            monitorInfoList[0]->rightTunnel.emplace_back(std::make_shared<TunnelInfo>(std::move(tunnelInfo)));
        }

        {
            TunnelInfo tunnelInfo = { 0 };
            tunnelInfo.id = 1;
            tunnelInfo.from = monitorInfoList[1]->top + 265;
            tunnelInfo.to = monitorInfoList[1]->bottom;
            tunnelInfo.relativeID = 0;
            monitorInfoList[1]->leftTunnel.emplace_back(std::make_shared<TunnelInfo>(std::move(tunnelInfo)));
        }

        {
            TunnelInfo tunnelInfo = { 0 };
            tunnelInfo.id = 2;
            tunnelInfo.from = monitorInfoList[1]->top;
            tunnelInfo.to = monitorInfoList[1]->top + 264;
            tunnelInfo.relativeID = 3;
            monitorInfoList[1]->leftTunnel.emplace_back(std::make_shared<TunnelInfo>(std::move(tunnelInfo)));
        }

        {
            TunnelInfo tunnelInfo = { 0 };
            tunnelInfo.id = 3;
            tunnelInfo.from = monitorInfoList[0]->top;
            tunnelInfo.to = monitorInfoList[0]->top;
            tunnelInfo.relativeID = -1;
            monitorInfoList[0]->rightTunnel.emplace_back(std::make_shared<TunnelInfo>(std::move(tunnelInfo)));
        }

        // 初始化螢幕資訊
        try
        {
            mouseEdgeManager.UpdateMonitorInfo(monitorInfoList);
        }
        catch (const std::exception& e)
        {
            std::cout << e.what() << std::endl;
            system("pause");
            return -1;
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