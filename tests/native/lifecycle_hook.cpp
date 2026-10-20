#include <Windows.h>
#include <atomic>
#include <cstdio>
#include <stdexcept>
#include <thread>

namespace
{
    // Stub hook installation so this test cannot intercept real keyboard or mouse input.
    std::atomic<HOOKPROC> mouseHook = nullptr;
    std::atomic_bool invokeMouse = false;
    HANDLE entered = nullptr;
    HANDLE releaseCallback = nullptr;
    HHOOK TestSetHook(int kind, HOOKPROC callback, HINSTANCE module, DWORD thread)
    {
        if (kind == WH_MOUSE_LL) mouseHook = callback;
        return reinterpret_cast<HHOOK>(1);
    }
    BOOL TestUnhook(HHOOK hook) { return TRUE; }
    LRESULT TestCallNext(HHOOK hook, int code, WPARAM wParam, LPARAM lParam) { return 0; }
    DWORD TestWait(HANDLE handle, DWORD timeout) { return WaitForSingleObject(handle, timeout == 5000 ? 40 : timeout); }
    BOOL TestPeek(LPMSG message, HWND window, UINT minimum, UINT maximum, UINT remove)
    {
        if (remove == PM_REMOVE && invokeMouse.exchange(false))
        {
            MSLLHOOKSTRUCT event{};
            mouseHook.load()(0, WM_MOUSEMOVE, reinterpret_cast<LPARAM>(&event));
        }
        return PeekMessageA(message, window, minimum, maximum, remove);
    }
    void Require(bool condition) { if (!condition) throw std::runtime_error("Hook lifetime regression failed"); }
}

#undef SetWindowsHookEx
#define SetWindowsHookEx TestSetHook
#define UnhookWindowsHookEx TestUnhook
#define CallNextHookEx TestCallNext
#undef PeekMessage
#define PeekMessage TestPeek
#define WaitForSingleObject TestWait
#include "../../MonitorEdgeTunnelLib/HookManager.cpp"
#undef WaitForSingleObject

int main()
{
    entered = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    releaseCallback = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    auto manager = new HookManager;
    manager->SetMouseMoveCallback([](POINT& point) {
        SetEvent(entered);
        WaitForSingleObject(releaseCallback, INFINITE);
        return false;
    });
    Require(manager->Start());
    invokeMouse = true;
    Require(WaitForSingleObject(entered, 4000) == WAIT_OBJECT_0);
    Require(!manager->Stop() && !manager->Start());
    std::atomic_bool destroyed = false;
    std::thread destroyer([&] { delete manager; destroyed = true; });
    Sleep(100);
    Require(!destroyed);
    SetEvent(releaseCallback);
    destroyer.join();
    Require(destroyed);
    HookManager restart;
    for (int index = 0; index < 3; ++index)
        Require(restart.Start() && restart.Stop());
    CloseHandle(entered);
    CloseHandle(releaseCallback);
    std::puts("Hook stop timeout retains ownership without installing system hooks");
}
