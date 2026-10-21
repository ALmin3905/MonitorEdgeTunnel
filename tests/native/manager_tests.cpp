#include "ManagerFixture.h"

using namespace ManagerFixture;
using ErrorCode = MonitorEdgeTunnelManagerErrorMsg;

namespace
{
    int displayCalls = 0;
    void DisplayCallback() { ++displayCalls; }
}

int main()
{
    auto& manager = MonitorEdgeTunnelManager::GetInstance();
    return RunTests({
        { "initialization wires services and permits default start", [&] {
            Check(state.loads >= 1 && state.messages && state.mouse, "settings and callbacks initialized");
            Reset(manager);
            Check(manager.Start() && manager.IsStart(), "start without a saved layout succeeds");
            Error(ErrorCode::Null);
            Check(state.appends == 0, "absent layout skips attachment");
            Check(manager.Stop() && !manager.IsStart(), "stop reports actual hook state");
        } },
        { "every start failure maps to its service error", [&] {
            Reset(manager);
            state.hookStop = false;
            Check(!manager.Start() && state.reads == 0, "cannot change configuration while hook stop fails");
            Error(ErrorCode::HookFail);
            Reset(manager);
            state.enumerate = false;
            Check(!manager.Start(), "discovery failure"); Error(ErrorCode::GetMonitorInfoFailed);
            Reset(manager);
            state.monitors.clear();
            Check(!manager.Start(), "no monitors"); Error(ErrorCode::NoMonitorInfo);
            Reset(manager);
            state.encode = false;
            Check(!manager.Start(), "layout encoding failure"); Error(ErrorCode::NoMonitorInfo);
            Reset(manager);
            manager.SetTunnelInfoListStruct("layout", {});
            state.append = false;
            Check(!manager.Start(), "append failure"); Error(ErrorCode::TunnelInfoError);
            Reset(manager);
            state.monitors[0].leftTunnel = { MouseFixture::Tunnel(-1) };
            Check(!manager.Start(), "mouse validation failure"); Error(ErrorCode::TunnelInfoError);
            Reset(manager);
            state.hookStart = false;
            Check(!manager.Start() && !manager.IsStart(), "hook start failure"); Error(ErrorCode::HookFail);
            state.hookStart = true;
            Check(manager.Start(), "explicit restart recovers"); Error(ErrorCode::Null);
        } },
        { "named settings use independent snapshots", [&] {
            Reset(manager);
            TunnelInfoListStruct input;
            input.forceForbidEdge = true;
            input.tunnelInfoList = { MouseFixture::Tunnel(7) };
            manager.SetTunnelInfoListStruct("A", input);
            input.tunnelInfoList[0].id = 8;
            manager.SetTunnelInfoListStruct("B", input);
            TunnelInfoListStruct output;
            Check(manager.GetTunnelInfoListStruct("A", output) && output.tunnelInfoList[0].id == 7, "set copies caller data");
            output.tunnelInfoList[0].id = 99;
            Check(manager.GetTunnelInfoListStruct("A", output) && output.tunnelInfoList[0].id == 7, "get returns independent data");
            Check(manager.GetTunnelInfoListStruct("B", output) && output.tunnelInfoList[0].id == 8, "keys remain independent");
            Check(!manager.GetTunnelInfoListStruct("missing", output) && output.tunnelInfoList.empty() && !output.forceForbidEdge,
                "missing named configuration clears output");
        } },
        { "current layout settings and error reset", [&] {
            Reset(manager);
            TunnelInfoListStruct output;
            Check(!manager.GetCurrentTunnelInfoListStruct(output), "missing current layout reported");
            Error(ErrorCode::NoSettingInfo);
            TunnelInfoListStruct input;
            input.forceForbidEdge = true;
            Check(manager.SetCurrentTunnelInfoListStruct(input) && manager.GetCurrentTunnelInfoListStruct(output) && output.forceForbidEdge,
                "current layout set and get");
            Error(ErrorCode::Null);
            state.encode = false;
            Check(!manager.SetCurrentTunnelInfoListStruct({}), "cannot set unknown current layout"); Error(ErrorCode::NoMonitorInfo);
            Check(!manager.GetCurrentTunnelInfoListStruct(output), "cannot get unknown current layout"); Error(ErrorCode::NoMonitorInfo);
            state.encode = true;
            Check(manager.GetCurrentTunnelInfoListStruct(output) && output.forceForbidEdge, "failed set preserves saved layout");
        } },
        { "selected settings control forced boundary behavior", [&] {
            Reset(manager);
            TunnelInfoListStruct settings;
            settings.forceForbidEdge = true;
            settings.tunnelInfoList = { MouseFixture::Tunnel(7) };
            manager.SetTunnelInfoListStruct("layout", settings);
            Check(manager.Start() && state.appends == 1 && state.appended[0].id == 7, "selected settings forwarded to attachment");
            POINT point{ -101, -50 };
            Check(state.mouse(point) && point.x == -100, "force flag reaches real mouse manager");
            settings.forceForbidEdge = false;
            manager.SetTunnelInfoListStruct("layout", settings);
            Check(manager.Start(), "restart applies updated flag");
            point = { -101, -50 };
            Check(!state.mouse(point), "disabled force flag preserves natural crossing");
        } },
        { "keycode registration forwards callback and failure", [&] {
            Reset(manager);
            DWORD observed = 0;
            Check(manager.SetKeycodeCallback('K', [&](DWORD key) { observed = key; return true; }), "key callback forwarded");
            Check(state.keyCode == 'K' && state.key('K') && observed == 'K', "key and return contract retained");
            state.keyUpdate = false;
            Check(!manager.SetKeycodeCallback('K', {}), "hook registration failure returned");
        } },
        { "display callback membership and stopped notifications", [&] {
            Reset(manager);
            displayCalls = 0;
            Check(manager.AddDisplayChangedCallback(DisplayCallback) && !manager.AddDisplayChangedCallback(DisplayCallback), "callback de-duplicated");
            const int before = state.reads;
            Notify();
            Check(displayCalls == 1 && state.reads == before, "stopped coordinator still delivers external notification");
            Check(manager.RemoveDisplayChangedCallback(DisplayCallback) && !manager.RemoveDisplayChangedCallback(DisplayCallback), "callback removal result");
            Notify();
            Check(displayCalls == 1, "removed display callback not invoked");
        } },
        { "failed manual start does not request automatic retries", [&] {
            Reset(manager);
            state.enumerate = false;
            Check(!manager.Start(), "manual start failure");
            const int before = state.reads;
            state.enumerate = true;
            Notify();
            Check(state.reads == before && state.pending == 0 && !manager.IsStart(), "unsuccessful enable leaves coordinator stopped");
            Check(manager.Start(), "explicit enable can recover");
        } },
        { "repost failure and manual stop cancel pending retry work", [&] {
            Reset(manager);
            Check(manager.Start(), "enable coordinator");
            state.enumerate = false;
            state.repost = false;
            Notify();
            Check(state.reposts == 1 && state.pending == 0 && !manager.IsStart(), "failed repost does not create pending work");
            state.repost = true;
            Notify();
            Check(state.pending == 1, "later display event may retry again");
            Check(manager.Stop(), "manual stop");
            const int before = state.reads;
            state.pending = 0;
            state.enumerate = true;
            Notify();
            Check(state.reads == before && !manager.IsStart(), "queued display event cannot re-enable after stop");
        } },
        { "stop failure reports actual state and clears enable intent", [&] {
            Reset(manager);
            Check(manager.Start(), "enable coordinator");
            state.hookStop = false;
            Check(!manager.Stop() && manager.IsStart(), "failed stop keeps actual running status visible"); Error(ErrorCode::HookFail);
            const int before = state.reads;
            state.hookStop = true;
            Notify();
            Check(state.reads == before, "manual stop intent prevents display-triggered restart");
            Check(manager.Stop(), "stop recovery"); Error(ErrorCode::Null);
        } },
        { "persistence exceptions map to distinct error codes", [&] {
            Reset(manager);
            state.saveFailure = true;
            Check(!manager.SaveSetting(), "save error reported"); Error(ErrorCode::SaveSettingFail);
            state.saveFailure = false;
            Check(manager.SaveSetting(), "save recovery"); Error(ErrorCode::Null);
            state.loadFailure = true;
            Check(!manager.LoadSetting(), "load error reported"); Error(ErrorCode::LoadSettingFail);
            state.loadFailure = false;
            manager.SetTunnelInfoListStruct("layout", {});
            state.loadMissing = true;
            Check(manager.LoadSetting(), "missing settings reload to empty defaults"); Error(ErrorCode::Null);
            { const auto settings = state.settings->TunnelInfoListStructMap.get_readonly(); Check(settings->empty(), "missing file clears applied layouts"); }
            state.loadMissing = false;
            Check(manager.LoadSetting(), "load recovery"); Error(ErrorCode::Null);
        } },
        { "error state is isolated between callers on different threads", [&] {
            Reset(manager);
            state.saveFailure = state.loadFailure = true;
            Check(!manager.SaveSetting(), "main thread save failure");
            ErrorCode initial = ErrorCode::SaveSettingFail, after = ErrorCode::Null;
            std::jthread other([&] {
                initial = manager.GetErrorMsgCode();
                manager.LoadSetting();
                after = manager.GetErrorMsgCode();
            });
            other.join();
            Check(initial == ErrorCode::Null && after == ErrorCode::LoadSettingFail, "worker has independent error state");
            Error(ErrorCode::SaveSettingFail);
        } },
        { "monitor discovery and logger registration are forwarded", [&] {
            Reset(manager);
            Check(manager.GetMonitorInfoList().size() == 1, "monitor list forwarded");
            state.enumerate = false;
            Check(manager.GetMonitorInfoList().empty(), "failed discovery produces no stale list");
            int calls = 0;
            manager.SetLogCallback([&](int, const char*) { ++calls; });
            Logger::Log(1, "coordinator logger forwarding");
            manager.SetLogCallback({});
            Check(calls == 1, "logger callback forwarded");
        } },
        { "shutdown stops window before hook and attempts both services", [&] {
            Reset(manager);
            Check(manager.Start(), "enable coordinator");
            state.shutdownOrder.clear();
            state.invokeOnStop = true;
            Check(manager.Shutdown(), "window callbacks during shutdown cannot deadlock coordinator lock");
            Check(state.shutdownOrder.front() == "window" && state.shutdownOrder.back() == "hook" && !manager.IsStart(),
                "window is stopped before final hook stop");
            state.invokeOnStop = false;
            state.windowStop = state.hookStop = false;
            state.shutdownOrder.clear();
            Check(!manager.Shutdown() && state.shutdownOrder == std::vector<std::string>{ "window", "hook" },
                "shutdown attempts both stops even if the first fails");
            state.windowStop = state.hookStop = true;
        } },
    });
}
