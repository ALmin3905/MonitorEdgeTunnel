#include "HookFixture.h"

using namespace HookFixture;

int main()
{
    return RunTests({
        { "initial lifecycle and idempotent calls", [] {
            HookManager manager;
            Check(!manager.IsRunning() && manager.Stop(), "idle manager stops successfully");
            const int before = installs;
            Check(manager.Start() && manager.IsRunning() && manager.Start(), "duplicate start succeeds");
            Check(installs == before + 2, "duplicate start installs no additional hooks");
            Check(manager.Stop() && !manager.IsRunning() && manager.Stop(), "duplicate stop succeeds");
        } },
        { "failed mouse or keyboard installation cleans up and recovers", [] {
            for (int kind : { WH_MOUSE_LL, WH_KEYBOARD_LL })
            {
                HookManager manager;
                const int before = removals;
                failKind = kind;
                Check(!manager.Start() && !manager.IsRunning(), "hook installation failure reported");
                Check(removals == before + (kind == WH_KEYBOARD_LL ? 1 : 0), "only installed hooks are removed");
                failKind = 0;
                Check(manager.Start() && manager.Stop(), "restart after installation failure");
            }
        } },
        { "mouse consumption and rewritten coordinates run on worker", [] {
            HookManager manager;
            std::atomic<DWORD> callbackThread = 0;
            Check(manager.SetMouseMoveCallback([&](POINT& point) {
                callbackThread = GetCurrentThreadId(); point = { -10, 200 }; return true;
            }), "set callback before start");
            Check(manager.Start(), "start mouse worker");
            const int before = warps;
            Check(Mouse() == -1, "consumed mouse event suppresses next hook");
            Check(warps == before + 1 && warped.x == -10 && warped.y == 200, "transformed coordinates passed to cursor API");
            Check(callbackThread == worker && callbackThread != GetCurrentThreadId(), "callback executes on hook thread");
            Check(manager.Stop(), "stop worker");
        } },
        { "mouse filters and callback replacement", [] {
            HookManager manager;
            Check(manager.Start(), "start worker");
            std::atomic_int calls = 0;
            Check(manager.SetMouseMoveCallback([&](POINT&) { ++calls; return false; }), "replace while running");
            const int before = warps;
            Check(Mouse(-1) == NextResult && Mouse(0, WM_LBUTTONDOWN) == NextResult && Mouse(0, WM_MOUSEMOVE, true) == NextResult,
                "negative hook code, wrong message, and null data pass through");
            Check(calls == 0, "filtered events do not call mouse callback");
            Check(Mouse() == NextResult && calls == 1 && warps == before, "unconsumed mouse event passes through");
            Check(manager.SetMouseMoveCallback({}), "null callback restores default");
            Check(Mouse() == NextResult && calls == 1, "removed callback is not invoked");
            Check(manager.Stop(), "stop worker");
        } },
        { "system keyboard dispatch and callback removal", [] {
            HookManager manager;
            std::atomic_int calls = 0;
            std::atomic<DWORD> observed = 0;
            manager.SetSysKeycodeCallback('A', [&](DWORD key) { observed = key; ++calls; return true; });
            Check(manager.Start(), "start keyboard worker");
            Check(Key('A') == -1 && observed == 'A' && calls == 1, "registered system key can consume event");
            Check(Key('B') == NextResult && Key('A', -1) == NextResult && Key('A', 0, WM_KEYDOWN) == NextResult &&
                Key('A', 0, WM_SYSKEYDOWN, true) == NextResult && calls == 1, "unregistered and filtered keys pass through");
            Check(manager.SetSysKeycodeCallback('A', [&](DWORD) { ++calls; return false; }), "replace key callback while running");
            Check(Key('A') == NextResult && calls == 2, "false return passes key through");
            Check(manager.SetSysKeycodeCallback('A', {}), "remove key callback while running");
            Check(Key('A') == NextResult && calls == 2, "removed key callback ignored");
            Check(manager.Stop(), "stop keyboard worker");
        } },
        { "post failure preserves existing callbacks and stop still succeeds", [] {
            HookManager manager;
            manager.SetMouseMoveCallback([](POINT&) { return true; });
            manager.SetSysKeycodeCallback('A', [](DWORD) { return true; });
            Check(manager.Start(), "start worker");
            failPost = true;
            Check(!manager.SetMouseMoveCallback({}) && Mouse() == -1, "failed mouse update retains old callback");
            failPost = true;
            Check(!manager.SetSysKeycodeCallback('A', {}) && Key('A') == -1, "failed key update retains old callback");
            failPost = true;
            Check(manager.Stop(), "stop request works even when wakeup message fails");
        } },
        { "callback may request stop without joining itself", [] {
            HookManager manager;
            std::atomic_bool stopResult = true, updateResult = true;
            manager.SetMouseMoveCallback([&](POINT&) {
                stopResult = manager.Stop();
                updateResult = manager.SetMouseMoveCallback({});
                return false;
            });
            Check(manager.Start() && Mouse() == NextResult, "callback requests orderly stop");
            Check(!stopResult && !updateResult, "worker stop defers join and rejects pending callback changes");
            Check(manager.Stop() && !manager.IsRunning(), "external caller joins requested stop");
        } },
    });
}
