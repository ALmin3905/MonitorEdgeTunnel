#include <Windows.h>
#include <cstdio>
#include <stdexcept>
#include "MonitorInfoManager.h"

namespace
{
    // Keep cursor discovery deterministic; these tests never move the system cursor.
    BOOL TestGetCursorPos(LPPOINT point) { *point = POINT{50, 50}; return TRUE; }
    void Require(bool condition) { if (!condition) throw std::runtime_error("Tunnel regression failed"); }
    MonitorInfo Monitor(int id, int left)
    {
        MonitorInfo monitor{};
        monitor.id = id; monitor.left = left; monitor.right = left + 99;
        monitor.bottom = 99; monitor.scaling = 1;
        return monitor;
    }
    TunnelInfo Tunnel(int id, int relative, EdgeType edge)
    {
        TunnelInfo tunnel{};
        tunnel.id = id; tunnel.relativeID = relative; tunnel.edgeType = edge;
        tunnel.rangeType = RangeType::Full;
        return tunnel;
    }
}

#define GetCursorPos TestGetCursorPos
#include "../../MonitorEdgeTunnelLib/MouseEdgeManager.cpp"
#undef GetCursorPos

int main()
{
    MouseEdgeManager manager;
    auto first = Monitor(0, 0);
    first.rightTunnel.push_back(Tunnel(1, -1, EdgeType::Right));
    manager.UpdateMonitorInfo({first}, false);
    POINT point{100, 50};
    Require(manager.EdgeTunnelTransport(point) && point.x == 99 && point.y == 50);

    manager.UpdateMonitorInfo({first}, true);
    point = POINT{-1, 50};
    Require(manager.EdgeTunnelTransport(point) && point.x == 0 && point.y == 50);

    auto second = Monitor(1, 100);
    first.rightTunnel = {Tunnel(2, 5, EdgeType::Right)};
    second.leftTunnel = {Tunnel(5, 2, EdgeType::Left)};
    manager.UpdateMonitorInfo({first, second}, false);
    point = POINT{100, 50};
    Require(manager.EdgeTunnelTransport(point) && point.x == 100 && point.y == 50);
    point = POINT{99, 50};
    Require(manager.EdgeTunnelTransport(point) && point.x == 99 && point.y == 50);
    std::puts("Sparse tunnel IDs preserve blocking and bidirectional transport");
}
