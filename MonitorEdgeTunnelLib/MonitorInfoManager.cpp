#include "pch.h"
#include "MonitorInfoManager.h"
#include "base64.h"
#include "Logger.h"
#include "Utility.h"
#include <Windows.h>
#include <memory>

bool MonitorInfoManager::GetMonitorInfoList(MonitorInfoList& result)
{
    result.clear();

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
        {
            LOG_WITH_CONTEXT(Logger::LogLevel::Error, "EnumDisplaySettings failed, DeviceName: " + displayDevice.DeviceName);
            return false;
        }

        rect.left = devMode.dmPosition.x;
        rect.right = devMode.dmPosition.x + devMode.dmPelsWidth - 1;
        rect.top = devMode.dmPosition.y;
        rect.bottom = devMode.dmPosition.y + devMode.dmPelsHeight - 1;
        monitor = MonitorFromRect(&rect, MONITOR_DEFAULTTONULL);
        if (!monitor)
        {
            LOG_WITH_CONTEXT(Logger::LogLevel::Error, "MonitorFromRect failed, " + Utility::to_string(rect));
            return false;
        }

        if (!GetMonitorInfo(monitor, &monitorInfoEx))
        {
            LOG_WITH_CONTEXT(Logger::LogLevel::Error, "GetMonitorInfo failed");
            return false;
        }

        MonitorInfo _info{};
        _info.id = increaseIndex++;
        _info.top = rect.top;
        _info.bottom = rect.bottom;
        _info.left = rect.left;
        _info.right = rect.right;
        _info.scaling = monitorInfoEx.rcMonitor.right > monitorInfoEx.rcMonitor.left ?
            static_cast<double>(devMode.dmPelsWidth) / static_cast<double>(monitorInfoEx.rcMonitor.right - monitorInfoEx.rcMonitor.left) :
            1.0;

        monitorInfoList.push_back(std::move(_info));
    }

    result = std::move(monitorInfoList);
    return true;
}

bool MonitorInfoManager::AppendTunnelInfoToMonitorInfo(MonitorInfoList& monitorInfoList, const TunnelInfoList& tunnelInfoList)
{
    for (const auto& tunnelInfo : tunnelInfoList)
    {
        bool ret = false;

        for (auto& monitorInfo : monitorInfoList)
        {
            if (tunnelInfo.displayID == monitorInfo.id)
            {
                switch (tunnelInfo.edgeType)
                {
                case EdgeType::Left:
                    monitorInfo.leftTunnel.push_back(tunnelInfo);
                    break;
                case EdgeType::Right:
                    monitorInfo.rightTunnel.push_back(tunnelInfo);
                    break;
                case EdgeType::Top:
                    monitorInfo.topTunnel.push_back(tunnelInfo);
                    break;
                case EdgeType::Bottom:
                    monitorInfo.bottomTunnel.push_back(tunnelInfo);
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

bool MonitorInfoManager::GetMonitorInfoListBase64(std::string& result)
{
    // init
    result.clear();

    // 螢幕資訊清單
    MonitorInfoList monitorInfoList;
    if (!GetMonitorInfoList(monitorInfoList))
        return false;

    return GetMonitorInfoListBase64(result, monitorInfoList);
}

bool MonitorInfoManager::GetMonitorInfoListBase64(std::string& result, const MonitorInfoList& monitorInfoList)
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
    const size_t bytesSize = (IdSize + TopSize + BottomSize + LeftSize + RightSize) * monitorInfoList.size();

    std::unique_ptr<unsigned char[]> bytes = std::make_unique<unsigned char[]>(bytesSize);
    ZeroMemory(bytes.get(), bytesSize);
    size_t size = 0;
    for (const auto& monitorInfo : monitorInfoList)
    {
        memcpy(bytes.get() + size, &monitorInfo.id, IdSize);
        size += IdSize;
        memcpy(bytes.get() + size, &monitorInfo.top, TopSize);
        size += TopSize;
        memcpy(bytes.get() + size, &monitorInfo.bottom, BottomSize);
        size += BottomSize;
        memcpy(bytes.get() + size, &monitorInfo.left, LeftSize);
        size += LeftSize;
        memcpy(bytes.get() + size, &monitorInfo.right, RightSize);
        size += RightSize;
    }

    // base64編碼
    result = base64_encode(bytes.get(), static_cast<uint32_t>(size));

    return true;
}
