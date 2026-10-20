#include "MouseFixture.h"

int main()
{
    return RunTests({
        { "maximum user ID with forced boundaries", [] {
            auto monitor = MouseFixture::Monitor(0);
            monitor.leftTunnel = { MouseFixture::Tunnel(100) };
            MouseEdgeManager manager;
            manager.UpdateMonitorInfo({ monitor }, true);
            POINT point{ 1, -50 };
            Check(manager.EdgeTunnelTransport(point), "generated rules do not consume the user ID range");
            MouseFixture::EqualPoint(point, 0, -50);
        } },
        { "many monitors still receive every forced boundary", [] {
            MonitorInfoList monitors;
            for (int id = 0; id < 26; ++id)
                monitors.push_back(MouseFixture::Monitor(id, -100 + 200 * id, -100, 200 * id, 0));
            MouseEdgeManager manager;
            manager.UpdateMonitorInfo(monitors, true);
            Check(manager.IsInit(), "generated boundaries are not restricted to 101 user IDs");
        } },
    });
}
