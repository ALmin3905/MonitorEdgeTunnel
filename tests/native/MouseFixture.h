#pragma once

#include <Windows.h>
#include "MouseEdgeManager.h"
#include "TestSupport.h"

namespace MouseFixture
{
    inline POINT cursor{ -50, -50 };
    inline bool cursorAvailable = true;

    // Supply a deterministic cursor without moving the system pointer.
    inline BOOL ReadCursor(LPPOINT output) { *output = cursor; return cursorAvailable ? TRUE : FALSE; }

    inline MonitorInfo Monitor(int id, int left = -100, int top = -100, int right = 0, int bottom = 0, double scale = 1)
    {
        MonitorInfo monitor{};
        monitor.id = id; monitor.left = left; monitor.top = top;
        monitor.right = right; monitor.bottom = bottom; monitor.scaling = scale;
        return monitor;
    }

    inline TunnelInfo Tunnel(int id, int relative = -1, RangeType range = RangeType::Full, int from = 0, int to = 0)
    {
        TunnelInfo tunnel{};
        tunnel.id = id; tunnel.relativeID = relative;
        tunnel.rangeType = range; tunnel.from = from; tunnel.to = to;
        return tunnel;
    }

    inline TunnelInfoList& Edge(MonitorInfo& monitor, EdgeType edge)
    {
        switch (edge)
        {
        case EdgeType::Left: return monitor.leftTunnel;
        case EdgeType::Right: return monitor.rightTunnel;
        case EdgeType::Top: return monitor.topTunnel;
        case EdgeType::Bottom: return monitor.bottomTunnel;
        }
        throw std::logic_error("Invalid test edge");
    }

    inline POINT Outside(const MonitorInfo& monitor, EdgeType edge, int along)
    {
        switch (edge)
        {
        case EdgeType::Left: return { monitor.left - 1, along };
        case EdgeType::Right: return { monitor.right + 1, along };
        case EdgeType::Top: return { along, monitor.top - 1 };
        case EdgeType::Bottom: return { along, monitor.bottom + 1 };
        }
        throw std::logic_error("Invalid test edge");
    }

    inline void EqualPoint(POINT actual, LONG x, LONG y)
    {
        Check(actual.x == x && actual.y == y, "unexpected transported coordinates");
    }
}

#define GetCursorPos MouseFixture::ReadCursor
#include "../../MonitorEdgeTunnelLib/MouseEdgeManager.cpp"
#undef GetCursorPos
