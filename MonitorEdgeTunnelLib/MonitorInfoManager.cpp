#include "pch.h"
#include "MonitorInfoManager.h"
#include "base64.h"
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

/*static*/ bool MonitorInfoManager::GetMonitorInfoListBase64(std::string& result)
{
    // init
    result.clear();

    // 螢幕資訊清單
    MonitorInfoList monitorInfoList;
    if (!GetMonitorInfoList(monitorInfoList))
        return false;

    return GetMonitorInfoListBase64(result, monitorInfoList);
}

/*static*/ bool MonitorInfoManager::GetMonitorInfoListBase64(std::string& result, const MonitorInfoList& monitorInfoList)
{
    // init
    result.clear();

    // check
    if (monitorInfoList.empty())
        return false;

    // bytes，只取id、上下左右
    constexpr size_t IdSize = sizeof(MonitorInfo::id);
    constexpr size_t TopSize = sizeof(MonitorInfo::top);
    constexpr size_t BottomSize = sizeof(MonitorInfo::bottom);
    constexpr size_t LeftSize = sizeof(MonitorInfo::left);
    constexpr size_t RightSize = sizeof(MonitorInfo::right);
    constexpr size_t bytesSize = IdSize + TopSize + BottomSize + LeftSize + RightSize;

    unsigned char* bytes = new unsigned char[bytesSize * monitorInfoList.size()];
    size_t size = 0;
    for (const auto& monitorInfo : monitorInfoList)
    {
        memcpy(bytes + size, &monitorInfo->id, IdSize);
        size += IdSize;
        memcpy(bytes + size, &monitorInfo->top, TopSize);
        size += TopSize;
        memcpy(bytes + size, &monitorInfo->bottom, BottomSize);
        size += BottomSize;
        memcpy(bytes + size, &monitorInfo->left, LeftSize);
        size += LeftSize;
        memcpy(bytes + size, &monitorInfo->right, RightSize);
        size += RightSize;
    }

    // base64編碼
    result = base64_encode(bytes, static_cast<uint32_t>(size));

    delete[] bytes;

    return true;
}