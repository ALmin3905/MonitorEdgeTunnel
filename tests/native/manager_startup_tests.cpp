#include "ManagerFixture.h"

int main()
{
    return RunTests({
        { "failed construction can be retried and load errors are nonfatal", [] {
            auto& state = ManagerFixture::state;
            state.bindMouse = false;
            CheckThrows<std::runtime_error>([] { MonitorEdgeTunnelManager::GetInstance(); }, "mouse binding failure aborts construction");
            state.bindMouse = true;
            state.windowStart = false;
            CheckThrows<std::runtime_error>([] { MonitorEdgeTunnelManager::GetInstance(); }, "window start failure aborts construction");
            state.windowStart = true;
            state.loadFailure = true;
            auto& manager = MonitorEdgeTunnelManager::GetInstance();
            ManagerFixture::Error(MonitorEdgeTunnelManagerErrorMsg::LoadSettingFail);
            state.loadFailure = false;
            Check(manager.Start() && manager.Shutdown(), "missing settings do not prevent successful service initialization");
        } },
    });
}
