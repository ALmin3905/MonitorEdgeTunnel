#include "MouseFixture.h"

using namespace MouseFixture;

int main()
{
    return RunTests({
        { "all 16 edge mappings including both endpoints", [] {
            for (int sourceEdge = 0; sourceEdge < 4; ++sourceEdge)
                for (int targetEdge = 0; targetEdge < 4; ++targetEdge)
                    for (int offset : { 0, 25, 50, 75, 100 })
                    {
                        auto source = Monitor(0);
                        auto target = Monitor(1, 200, 400, 400, 600);
                        Edge(source, static_cast<EdgeType>(sourceEdge)).push_back(Tunnel(3, 90));
                        Edge(target, static_cast<EdgeType>(targetEdge)).push_back(Tunnel(90, 3));
                        MouseEdgeManager manager;
                        manager.UpdateMonitorInfo({ source, target }, false);
                        POINT point = Outside(source, static_cast<EdgeType>(sourceEdge), -100 + offset);
                        Check(manager.EdgeTunnelTransport(point), "full-edge transport must match");
                        switch (static_cast<EdgeType>(targetEdge))
                        {
                        case EdgeType::Left: EqualPoint(point, 200, 400 + offset * 2); break;
                        case EdgeType::Right: EqualPoint(point, 400, 400 + offset * 2); break;
                        case EdgeType::Top: EqualPoint(point, 200 + offset * 2, 400); break;
                        case EdgeType::Bottom: EqualPoint(point, 200 + offset * 2, 600); break;
                        }
                    }
        } },
        { "inside and exact edges remain unchanged", [] {
            MouseEdgeManager manager;
            Check(!manager.IsInit(), "new manager uninitialized");
            manager.UpdateMonitorInfo({ Monitor(0) });
            for (POINT point : { POINT{ -50, -50 }, POINT{ -100, -50 }, POINT{ 0, -50 }, POINT{ -50, -100 }, POINT{ -50, 0 } })
            {
                const POINT original = point;
                Check(!manager.EdgeTunnelTransport(point), "only points beyond inclusive edges are transported");
                EqualPoint(point, original.x, original.y);
            }
        } },
        { "forced boundaries block all four edges and corners", [] {
            MouseEdgeManager manager;
            const auto monitor = Monitor(0);
            manager.UpdateMonitorInfo({ monitor });
            for (int edge = 0; edge < 4; ++edge)
            {
                POINT point = Outside(monitor, static_cast<EdgeType>(edge), -50);
                Check(manager.EdgeTunnelTransport(point), "default blocking edge");
                EqualPoint(point, edge == 0 ? -100 : edge == 1 ? 0 : -50,
                    edge == 2 ? -100 : edge == 3 ? 0 : -50);
            }
            for (POINT point : { POINT{ -102, -101 }, POINT{ 1, -102 }, POINT{ -101, 2 }, POINT{ 1, 1 } })
            {
                const LONG x = point.x < -100 ? -100 : 0;
                const LONG y = point.y < -100 ? -100 : 0;
                Check(manager.EdgeTunnelTransport(point), "diagonal movement is clamped to a corner");
                EqualPoint(point, x, y);
            }
        } },
        { "relative offsets and custom lengths on every edge", [] {
            for (int edge = 0; edge < 4; ++edge)
                for (auto range : { RangeType::Relative, RangeType::Customize })
                {
                    auto monitor = Monitor(0);
                    Edge(monitor, static_cast<EdgeType>(edge)).push_back(Tunnel(0, -1, range, 10,
                        range == RangeType::Relative ? 20 : 70));
                    MouseEdgeManager manager;
                    manager.UpdateMonitorInfo({ monitor }, false);
                    for (int along : { -91, -90, -55, -20, -19 })
                    {
                        auto point = Outside(monitor, static_cast<EdgeType>(edge), along);
                        const bool matches = along >= -90 && along <= -20;
                        Check(manager.EdgeTunnelTransport(point) == matches, "inclusive configured interval");
                        if (matches)
                            EqualPoint(point, edge == 0 ? -100 : edge == 1 ? 0 : along,
                                edge == 2 ? -100 : edge == 3 ? 0 : along);
                    }
                }
        } },
        { "explicit tunnels precede forced fallback boundaries", [] {
            auto source = Monitor(0);
            auto target = Monitor(1, 200, 400, 400, 600);
            source.leftTunnel.push_back(Tunnel(0, 1, RangeType::Customize, 25, 50));
            target.topTunnel.push_back(Tunnel(1));
            MouseEdgeManager manager;
            manager.UpdateMonitorInfo({ source, target }, true);
            POINT blocked{ -101, -90 };
            Check(manager.EdgeTunnelTransport(blocked), "outside configured range uses forced boundary");
            EqualPoint(blocked, -100, -90);
            POINT transported{ -101, -50 };
            Check(manager.EdgeTunnelTransport(transported), "explicit tunnel overrides appended boundary");
            EqualPoint(transported, 300, 400);
        } },
        { "first matching rule wins", [] {
            auto source = Monitor(0);
            auto target = Monitor(1, 200, 400, 400, 600);
            source.leftTunnel = { Tunnel(0, -1, RangeType::Customize, 10, 20), Tunnel(1, 2) };
            target.topTunnel = { Tunnel(2) };
            MouseEdgeManager manager;
            manager.UpdateMonitorInfo({ source, target }, false);
            POINT point{ -101, -80 };
            Check(manager.EdgeTunnelTransport(point), "overlap matches");
            EqualPoint(point, -100, -80);
        } },
        { "destination scaling is applied to both coordinates", [] {
            auto source = Monitor(0);
            auto target = Monitor(1, 200, 400, 400, 600, 2);
            source.leftTunnel = { Tunnel(0, 1) };
            target.rightTunnel = { Tunnel(1) };
            MouseEdgeManager manager;
            manager.UpdateMonitorInfo({ source, target }, false);
            POINT point{ -101, -50 };
            Check(manager.EdgeTunnelTransport(point), "scaled transport");
            EqualPoint(point, 200, 250);
            source.scaling = 2;
            source.leftTunnel = { Tunnel(0) };
            manager.UpdateMonitorInfo({ source }, false);
            point = { -101, -50 };
            Check(manager.EdgeTunnelTransport(point), "scaled blocking");
            EqualPoint(point, -50, -25);
        } },
        { "crossing without a tunnel updates current monitor", [] {
            auto source = Monitor(0);
            auto adjacent = Monitor(1, 1, -100, 101, 0);
            adjacent.rightTunnel = { Tunnel(0) };
            MouseEdgeManager manager;
            manager.UpdateMonitorInfo({ source, adjacent }, false);
            POINT point{ 50, -50 };
            Check(!manager.EdgeTunnelTransport(point), "natural crossing does not request a cursor warp");
            point = { 102, -50 };
            Check(manager.EdgeTunnelTransport(point), "next movement uses the newly entered monitor");
            EqualPoint(point, 101, -50);
        } },
        { "linked crossing updates current monitor and allows return", [] {
            auto source = Monitor(0);
            auto target = Monitor(1, 200, 400, 400, 600);
            source.rightTunnel = { Tunnel(0, 1) };
            target.leftTunnel = { Tunnel(1, 0) };
            MouseEdgeManager manager;
            manager.UpdateMonitorInfo({ source, target }, false);
            POINT point{ 1, -50 };
            Check(manager.EdgeTunnelTransport(point), "forward crossing");
            EqualPoint(point, 200, 500);
            point = { 199, 500 };
            Check(manager.EdgeTunnelTransport(point), "reverse crossing uses destination as current");
            EqualPoint(point, 0, -50);
        } },
        { "updates copy caller data and reset initialization after failure", [] {
            MonitorInfoList monitors{ Monitor(0) };
            MouseEdgeManager manager;
            manager.UpdateMonitorInfo(monitors);
            Check(monitors[0].leftTunnel.empty(), "generated boundaries do not change caller data");
            monitors[0].left = -500;
            POINT point{ -101, -50 };
            Check(manager.EdgeTunnelTransport(point), "caller mutation cannot invalidate internal pointers");
            EqualPoint(point, -100, -50);
            CheckThrows<std::runtime_error>([&] { manager.UpdateMonitorInfo({}); }, "empty monitor list rejected");
            Check(!manager.IsInit(), "failed update clears initialized state");
            manager.UpdateMonitorInfo({ Monitor(0) });
            Check(manager.IsInit(), "valid update recovers");
        } },
        { "invalid IDs, references, and ranges rejected", [] {
            auto reject = [](TunnelInfo tunnel) {
                auto monitor = Monitor(0);
                monitor.leftTunnel = { tunnel };
                MouseEdgeManager manager;
                CheckThrows<std::runtime_error>([&] { manager.UpdateMonitorInfo({ monitor }, false); }, "invalid tunnel accepted");
                Check(!manager.IsInit(), "invalid tunnel must not initialize manager");
            };
            reject(Tunnel(-1)); reject(Tunnel(101));
            reject(Tunnel(0, -2)); reject(Tunnel(0, 0)); reject(Tunnel(0, 100));
            reject(Tunnel(0, -1, static_cast<RangeType>(99)));
            reject(Tunnel(0, -1, RangeType::Relative, -1, 0));
            reject(Tunnel(0, -1, RangeType::Relative, 60, 60));
            reject(Tunnel(0, -1, RangeType::Customize, 90, 20));
            auto monitor = Monitor(0);
            monitor.leftTunnel = { Tunnel(4), Tunnel(4) };
            MouseEdgeManager manager;
            CheckThrows<std::runtime_error>([&] { manager.UpdateMonitorInfo({ monitor }); }, "duplicate ID rejected");
        } },
        { "single-point blocking remains valid", [] {
            auto monitor = Monitor(0);
            monitor.leftTunnel = { Tunnel(100, -1, RangeType::Customize, 50, 0) };
            MouseEdgeManager manager;
            manager.UpdateMonitorInfo({ monitor }, false);
            POINT point{ -101, -50 };
            Check(manager.EdgeTunnelTransport(point), "single point can forbid crossing");
            EqualPoint(point, -100, -50);
        } },
    });
}
