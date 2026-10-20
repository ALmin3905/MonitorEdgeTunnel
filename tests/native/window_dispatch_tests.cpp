#include "WindowFixture.h"

using namespace WindowFixture;

int main()
{
    return RunTests({
        { "idle and repeated lifecycle cleans hidden windows", [] {
            WindowMessageManager manager;
            Check(!manager.IsRunning() && !manager.RePostDisplayChangeEvent() && manager.Stop(), "idle behavior");
            const int before = registrations;
            const int removed = unregistrations;
            for (int i = 0; i < 3; ++i)
            {
                Check(manager.Start() && manager.Start() && manager.IsRunning(), "idempotent start");
                const HWND created = window;
                Check(created && IsWindow(created), "hidden window exists");
                Check(manager.Stop() && manager.Stop() && !manager.IsRunning(), "idempotent stop");
                Check(!IsWindow(created) && !manager.RePostDisplayChangeEvent(), "stopped window is destroyed");
            }
            Check(registrations == before + 3 && unregistrations == removed + 3, "every window class released");
        } },
        { "display events reset a one-second debounce timer", [] {
            WindowMessageManager manager;
            std::atomic_int calls = 0;
            std::atomic<DWORD> callbackThread = 0;
            auto callback = [&] { callbackThread = GetCurrentThreadId(); ++calls; };
            manager.DisplayChangedDelegate.Add(&callback);
            Check(manager.Start(), "start dispatch worker");
            const int before = timers;
            const int cancelled = cancellations;
            for (int i = 0; i < 5; ++i) Check(manager.RePostDisplayChangeEvent(), "repost accepted");
            Check(WaitUntil([&] { return timers >= before + 5; }), "all reposts reached hidden window");
            Check(requestedDelay == 1000 && cancellations >= cancelled + 5 && calls == 0,
                "each event resets delay instead of invoking subscribers immediately");
            Fire(99);
            Fire();
            Check(WaitUntil([&] { return calls == 1; }), "known timer dispatches one notification");
            Check(callbackThread != GetCurrentThreadId(), "display callback runs on message worker");
            Check(manager.Stop() && calls == 1, "unknown timer does not notify subscribers");
        } },
        { "native display event and subscriber removal", [] {
            WindowMessageManager manager;
            std::atomic_int first = 0, second = 0;
            auto one = [&] { ++first; };
            auto two = [&] { ++second; };
            manager.DisplayChangedDelegate.Add(&one);
            manager.DisplayChangedDelegate.Add(&two);
            Check(manager.Start(), "start native message test");
            const int before = timers;
            Check(PostMessageW(window, WM_DISPLAYCHANGE, 0, 0), "send display event to test window only");
            Check(WaitUntil([&] { return timers > before; }), "native display event scheduled timer");
            Fire();
            Check(WaitUntil([&] { return first == 1 && second == 1; }), "both subscribers notified");
            Check(manager.DisplayChangedDelegate.Remove(&one), "remove subscriber");
            Fire();
            Check(WaitUntil([&] { return second == 2; }) && first == 1, "removed callback ignored");
            Check(manager.Stop(), "stop native message test");
        } },
        { "repost failure reports false and permits recovery", [] {
            WindowMessageManager manager;
            Check(manager.Start(), "start repost test");
            failPost = true;
            Check(!manager.RePostDisplayChangeEvent(), "OS post failure reported");
            Check(manager.RePostDisplayChangeEvent() && manager.Stop(), "later repost and stop succeed");
        } },
        { "worker callback may request stop", [] {
            WindowMessageManager manager;
            std::atomic_bool called = false, result = true;
            auto callback = [&] { result = manager.Stop(); called = true; };
            manager.DisplayChangedDelegate.Add(&callback);
            Check(manager.Start(), "start callback stop test");
            Fire();
            Check(WaitUntil([&] { return called.load(); }) && !result, "worker requests stop without self-join");
            Check(manager.Stop() && !manager.IsRunning(), "external caller completes join");
        } },
        { "class registration failure can be retried", [] {
            WindowMessageManager manager;
            failRegister = true;
            const bool started = manager.Start();
            failRegister = false;
            Check(!started && !manager.IsRunning(), "registration failure reported");
            Check(manager.Start() && manager.Stop(), "registration failure does not poison restart");
        } },
    });
}
