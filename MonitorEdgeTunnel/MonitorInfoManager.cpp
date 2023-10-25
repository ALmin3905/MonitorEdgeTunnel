#include "MonitorInfoManager.h"
#include <Windows.h>

/*static*/ bool MonitorInfoManager::GetMonitorInfoList(MonitorInfoList& result)
{
    MonitorInfoList monitorInfoList;

    DISPLAY_DEVICE displayDevice{};
    displayDevice.cb = sizeof(DISPLAY_DEVICE);
    DEVMODE devMode{};
    devMode.dmSize = sizeof(DEVMODE);
    devMode.dmDriverExtra = 0;
    HMONITOR monitor;
    MONITORINFOEX monitorInfoEx{};
    monitorInfoEx.cbSize = sizeof(MONITORINFOEX);
    RECT rect{};

    int increaseIndex = 0;
    for (int i = 0;;i++) {
        if (!EnumDisplayDevices(NULL, i, &displayDevice, 0))
            break;

        if (!(displayDevice.StateFlags & DISPLAY_DEVICE_ACTIVE))
            continue;

        if (!EnumDisplaySettings(displayDevice.DeviceName, ENUM_CURRENT_SETTINGS, &devMode))
            return false;

        rect.left = devMode.dmPosition.x;
        rect.right = devMode.dmPosition.x + devMode.dmPelsWidth - 1;
        rect.top = devMode.dmPosition.y;
        rect.bottom = devMode.dmPosition.y + devMode.dmPelsHeight - 1;
        monitor = MonitorFromRect(&rect, MONITOR_DEFAULTTONULL);
        if (!monitor)
            return false;

        if (!GetMonitorInfo(monitor, &monitorInfoEx))
            return false;

        MonitorInfo _info = { 0 };
        _info.id = increaseIndex++;
        _info.top = rect.top;
        _info.bottom = rect.bottom;
        _info.left = rect.left;
        _info.right = rect.right;
        _info.scaling = static_cast<double>(devMode.dmPelsWidth) / static_cast<double>(monitorInfoEx.rcMonitor.right - monitorInfoEx.rcMonitor.left);

        monitorInfoList.emplace_back(std::make_shared<MonitorInfo>(std::move(_info)));
    }

    result = std::move(monitorInfoList);
    return true;
}

/*static*/ bool MonitorInfoManager::AppendTunnelInfoToMonitorInfo(MonitorInfoList& monitorInfoList, TunnelInfoList& tunnelInfoList)
{
    for (const auto& tunnelInfo : tunnelInfoList)
    {
        bool ret = false;

        for (auto& monitorInfo : monitorInfoList)
        {
            if (tunnelInfo->displayID == monitorInfo->id)
            {
                switch (tunnelInfo->edgeType)
                {
                case EdgeType::Left:
                    monitorInfo->leftTunnel.push_back(tunnelInfo);
                    break;
                case EdgeType::Right:
                    monitorInfo->rightTunnel.push_back(tunnelInfo);
                    break;
                case EdgeType::Top:
                    monitorInfo->topTunnel.push_back(tunnelInfo);
                    break;
                case EdgeType::Bottom:
                    monitorInfo->bottomTunnel.push_back(tunnelInfo);
                    break;
                default:
                    return false;
                }

                ret = true;
                break;
            }
        }

        if (!ret)
            return false;
    }

    return true;
}