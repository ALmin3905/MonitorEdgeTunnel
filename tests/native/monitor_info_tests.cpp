#include <Windows.h>
#include <shellscalingapi.h>
#include <algorithm>
#include <cstring>
#include <string>
#include <vector>
#include "MonitorInfoManager.h"
#include "TestSupport.h"

namespace
{
    // Script display discovery without reading or modifying the user's actual monitors.
    struct Device
    {
        bool active;
        LONG x, y;
        DWORD width, height;
        LONG logicalWidth;
        UINT dpi = 96;
    };
    std::vector<Device> devices;
    int failure = 0;
    int failureIndex = -1;
    size_t selected = 0;
    RECT discovered{};
    // Model thread DPI state and independent failures for display-only queries.
    DPI_AWARENESS_CONTEXT threadContext = DPI_AWARENESS_CONTEXT_SYSTEM_AWARE;
    int dpiFailure = 0;

    BOOL TestEnumDevices(LPCSTR, DWORD index, PDISPLAY_DEVICEA output, DWORD)
    {
        if (index >= devices.size()) return FALSE;
        *output = {};
        output->cb = sizeof(*output);
        strcpy_s(output->DeviceName, std::to_string(index).c_str());
        output->StateFlags = devices[index].active ? DISPLAY_DEVICE_ACTIVE : 0;
        return TRUE;
    }
    BOOL TestEnumSettings(LPCSTR name, DWORD, DEVMODEA* output)
    {
        selected = static_cast<size_t>(std::stoi(name));
        if (failure == 1 && (failureIndex < 0 || selected == failureIndex)) return FALSE;
        const auto& device = devices[selected];
        output->dmPosition = { device.x, device.y };
        output->dmPelsWidth = device.width;
        output->dmPelsHeight = device.height;
        return TRUE;
    }
    HMONITOR TestMonitorFromRect(LPCRECT rect, DWORD)
    {
        discovered = *rect;
        return failure == 2 && (failureIndex < 0 || selected == failureIndex) ? nullptr : reinterpret_cast<HMONITOR>(selected + 1);
    }
    BOOL TestMonitorInfo(HMONITOR, MONITORINFOEXA* output)
    {
        if (failure == 3 && (failureIndex < 0 || selected == failureIndex)) return FALSE;
        output->rcMonitor = { 0, 0, devices[selected].logicalWidth, 100 };
        return TRUE;
    }
    DPI_AWARENESS_CONTEXT TestSetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT context)
    {
        if (dpiFailure == 1 && context == DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE)
            return nullptr;
        const auto previous = threadContext;
        threadContext = context;
        return previous;
    }
    HMONITOR TestMonitorFromPoint(POINT point, DWORD flags)
    {
        Check(threadContext == DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE, "display lookup uses physical coordinates");
        Check(flags == MONITOR_DEFAULTTONULL, "missing display must not fall back to another monitor");
        if (dpiFailure == 2) return nullptr;
        for (size_t index = 0; index < devices.size(); ++index)
        {
            if (devices[index].active && point.x == devices[index].x && point.y == devices[index].y)
                return reinterpret_cast<HMONITOR>(index + 1);
        }
        return nullptr;
    }
    HRESULT TestGetDpiForMonitor(HMONITOR monitor, MONITOR_DPI_TYPE type, UINT* dpiX, UINT* dpiY)
    {
        Check(threadContext == DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE, "query returns per-monitor DPI");
        Check(type == MDT_EFFECTIVE_DPI, "user-selected display scaling uses effective DPI");
        if (dpiFailure == 3) return E_FAIL;
        *dpiX = *dpiY = dpiFailure == 4 ? 0 : devices[reinterpret_cast<size_t>(monitor) - 1].dpi;
        return S_OK;
    }
    void Reset(std::vector<Device> input)
    {
        devices = std::move(input);
        failure = 0;
        failureIndex = -1;
        dpiFailure = 0;
        threadContext = DPI_AWARENESS_CONTEXT_SYSTEM_AWARE;
    }
    MonitorInfo Monitor(int id, int top, int bottom, int left, int right)
    {
        MonitorInfo value{};
        value.id = id; value.top = top; value.bottom = bottom;
        value.left = left; value.right = right; value.scaling = 1;
        return value;
    }
}

#undef EnumDisplayDevices
#undef EnumDisplaySettings
#undef GetMonitorInfo
#define EnumDisplayDevices TestEnumDevices
#define EnumDisplaySettings TestEnumSettings
#define MonitorFromRect TestMonitorFromRect
#define GetMonitorInfo TestMonitorInfo
#define SetThreadDpiAwarenessContext TestSetThreadDpiAwarenessContext
#define MonitorFromPoint TestMonitorFromPoint
#define GetDpiForMonitor TestGetDpiForMonitor
#include "../../MonitorEdgeTunnelLib/MonitorInfoManager.cpp"
#undef EnumDisplayDevices
#undef EnumDisplaySettings
#undef MonitorFromRect
#undef GetMonitorInfo
#undef SetThreadDpiAwarenessContext
#undef MonitorFromPoint
#undef GetDpiForMonitor
#include "../../MonitorEdgeTunnelLib/base64.cpp"

int main()
{
    return RunTests({
        { "empty discovery clears previous output", [] {
            Reset({});
            MonitorInfoList output{ Monitor(7, 0, 99, 0, 99) };
            Check(MonitorInfoManager::GetMonitorInfoList(output) && output.empty(), "no active displays is an empty success");
        } },
        { "active devices receive consecutive IDs", [] {
            Reset({ { true, -200, -100, 200, 100, 100 }, { false, 0, 0, 100, 100, 100 },
                { true, 0, 0, 300, 200, 300 } });
            MonitorInfoList output;
            Check(MonitorInfoManager::GetMonitorInfoList(output) && output.size() == 2, "inactive device skipped");
            Check(output[0].id == 0 && output[1].id == 1, "IDs have no inactive-device gap");
            Check(output[0].left == -200 && output[0].right == -1 && output[0].top == -100 && output[0].bottom == -1,
                "negative origin and inclusive last pixel");
            Check(output[0].scaling == 2 && output[1].scaling == 1, "physical-to-logical scale");
            Check(discovered.right == 299 && discovered.bottom == 199, "monitor lookup receives inclusive rectangle");
            Check(output[0].leftTunnel.empty() && output[1].bottomTunnel.empty(), "discovery contains no settings");
        } },
        { "display scaling is independent of mouse coordinate compensation", [] {
            for (const auto context : { DPI_AWARENESS_CONTEXT_UNAWARE, DPI_AWARENESS_CONTEXT_SYSTEM_AWARE,
                DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 })
            {
                Reset({ { true, -1920, 0, 1920, 1080, 3360, 96 }, { true, 0, 0, 3840, 2160, 3840, 168 } });
                MonitorInfoList output;
                Check(MonitorInfoManager::GetMonitorInfoList(output) && output.size() == 2, "mixed DPI discovery");
                threadContext = context;
                Check(MonitorInfoManager::GetDisplayScaling(output[0]) == 1 &&
                    MonitorInfoManager::GetDisplayScaling(output[1]) == 1.75, "100 and 175 percent display scaling");
                Check(threadContext == context, "caller DPI context restored after successful query");
                Check(output[0].scaling == 1920.0 / 3360 && output[1].scaling == 1,
                    "mouse coordinate compensation is preserved");
            }
        } },
        { "display DPI query failures restore caller state and report unknown", [] {
            for (int stage : { 1, 2, 3, 4 })
            {
                Reset({ { true, 0, 0, 1920, 1080, 1920, 120 } });
                dpiFailure = stage;
                MonitorInfo monitor = Monitor(0, 0, 1079, 0, 1919);
                Check(MonitorInfoManager::GetDisplayScaling(monitor) == 0, "failure must not claim 100 percent");
                Check(threadContext == DPI_AWARENESS_CONTEXT_SYSTEM_AWARE, "DPI context restored on query failure");
            }
        } },
        { "invalid logical width uses unit scale", [] {
            for (LONG width : { 0L, -1L })
            {
                Reset({ { true, 0, 0, 100, 100, width } });
                MonitorInfoList output;
                Check(MonitorInfoManager::GetMonitorInfoList(output) && output[0].scaling == 1, "zero/negative logical width fallback");
            }
        } },
        { "discovery failures discard partial output", [] {
            for (int stage : { 1, 2, 3 })
            {
                for (int device : { 0, 1 })
                {
                    Reset({ { true, 0, 0, 100, 100, 100 }, { true, 100, 0, 100, 100, 100 } });
                    failure = stage;
                    failureIndex = device;
                    MonitorInfoList output{ Monitor(7, 0, 99, 0, 99) };
                    Check(!MonitorInfoManager::GetMonitorInfoList(output) && output.empty(), "failed discovery must not expose stale data");
                }
            }
        } },
        { "append routes every edge by monitor ID", [] {
            MonitorInfoList monitors{ Monitor(7, 0, 99, 0, 99), Monitor(3, 0, 99, 100, 199) };
            TunnelInfoList tunnels;
            for (int edge = 0; edge < 4; ++edge)
            {
                TunnelInfo tunnel{};
                tunnel.id = edge; tunnel.displayID = 3; tunnel.edgeType = static_cast<EdgeType>(edge);
                tunnel.from = 10; tunnel.to = 20; tunnel.relativeID = -1; tunnel.rangeType = RangeType::Customize;
                tunnels.push_back(tunnel);
            }
            Check(MonitorInfoManager::AppendTunnelInfoToMonitorInfo(monitors, tunnels), "all edge routes accepted");
            Check(monitors[0].leftTunnel.empty(), "routing uses ID, not vector position");
            const std::vector<const TunnelInfoList*> edges{ &monitors[1].leftTunnel, &monitors[1].rightTunnel,
                &monitors[1].topTunnel, &monitors[1].bottomTunnel };
            for (int edge = 0; edge < 4; ++edge)
                Check(edges[edge]->size() == 1 && edges[edge]->front().id == edge && edges[edge]->front().from == 10,
                    "all tunnel fields copied to matching edge");
            Check(tunnels[0].displayID == 3, "caller tunnel list unchanged");
            Check(MonitorInfoManager::AppendTunnelInfoToMonitorInfo(monitors, {}), "empty append succeeds");
        } },
        { "append rejects missing displays and invalid edges", [] {
            MonitorInfoList monitors{ Monitor(0, 0, 99, 0, 99) };
            TunnelInfo tunnel{};
            tunnel.displayID = 4;
            Check(!MonitorInfoManager::AppendTunnelInfoToMonitorInfo(monitors, { tunnel }), "missing display");
            tunnel.displayID = 0; tunnel.edgeType = static_cast<EdgeType>(99);
            Check(!MonitorInfoManager::AppendTunnelInfoToMonitorInfo(monitors, { tunnel }), "invalid edge");
        } },
        { "layout key has stable little-endian field order", [] {
            std::string key;
            MonitorInfoList monitors{ Monitor(1, 2, 3, 4, 5) };
            Check(MonitorInfoManager::GetMonitorInfoListBase64(key, monitors), "nonempty layout encoded");
            Check(key == "AQAAAAIAAAADAAAABAAAAAUAAAA=", "ID, top, bottom, left, right binary layout");
            const auto original = key;
            monitors[0].scaling = 2;
            monitors[0].leftTunnel.push_back(TunnelInfo{});
            MonitorInfoManager::GetMonitorInfoListBase64(key, monitors);
            Check(key == original, "scale and settings excluded from layout key");
            monitors[0].left = -1;
            MonitorInfoManager::GetMonitorInfoListBase64(key, monitors);
            Check(key == "AQAAAAIAAAADAAAA/////wUAAAA=", "signed coordinate representation");
        } },
        { "layout key is sensitive to order and geometry", [] {
            MonitorInfoList monitors{ Monitor(0, 0, 99, 0, 99), Monitor(1, -100, -1, -100, -1) };
            std::string original, changed;
            MonitorInfoManager::GetMonitorInfoListBase64(original, monitors);
            std::reverse(monitors.begin(), monitors.end());
            MonitorInfoManager::GetMonitorInfoListBase64(changed, monitors);
            Check(changed != original, "enumeration order included");
            std::reverse(monitors.begin(), monitors.end());
            ++monitors[0].right;
            MonitorInfoManager::GetMonitorInfoListBase64(changed, monitors);
            Check(changed != original, "one-pixel layout change included");
        } },
        { "layout key overload clears output on failure", [] {
            std::string key = "stale";
            Check(!MonitorInfoManager::GetMonitorInfoListBase64(key, {}) && key.empty(), "empty layout rejected");
            Reset({ { true, 0, 0, 100, 100, 100 } });
            failure = 1;
            key = "stale";
            Check(!MonitorInfoManager::GetMonitorInfoListBase64(key) && key.empty(), "discovery failure rejected");
            failure = 0;
            Check(MonitorInfoManager::GetMonitorInfoListBase64(key) && !key.empty(), "discover-and-encode success");
        } },
    });
}
