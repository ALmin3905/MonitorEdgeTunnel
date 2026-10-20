#pragma once

#include <Windows.h>
#include <atomic>
#include "WindowMessageManager.h"
#include "TestSupport.h"

namespace WindowFixture
{
    // Keep real hidden-window dispatch while controlling timers and injected OS failures.
    inline std::atomic<HWND> window = nullptr;
    inline std::atomic_bool failRegister = false, failCreate = false, failPost = false;
    inline std::atomic_int registrations = 0, unregistrations = 0, timers = 0, cancellations = 0;
    inline std::atomic<UINT> requestedDelay = 0;

    inline ATOM Register(const WNDCLASSW* descriptor)
    {
        if (failRegister) return 0;
        const auto result = RegisterClassW(descriptor);
        if (result) ++registrations;
        return result;
    }
    inline BOOL Unregister(LPCWSTR name, HINSTANCE instance)
    {
        const auto result = UnregisterClassW(name, instance);
        if (result) ++unregistrations;
        return result;
    }
    inline HWND Create(LPCWSTR className, LPCWSTR name, DWORD style, int x, int y, int width, int height,
        HWND parent, HMENU menu, HINSTANCE instance, LPVOID data)
    {
        if (failCreate) return nullptr;
        const auto result = CreateWindowExW(0, className, name, style, x, y, width, height, parent, menu, instance, data);
        window = result;
        return result;
    }
    inline UINT_PTR Timer(HWND, UINT_PTR id, UINT delay, TIMERPROC)
    {
        requestedDelay = delay;
        ++timers;
        return id;
    }
    inline BOOL Cancel(HWND, UINT_PTR) { ++cancellations; return TRUE; }
    inline BOOL Post(HWND target, UINT message, WPARAM wParam, LPARAM lParam)
    {
        if (failPost.exchange(false)) return FALSE;
        return PostMessageA(target, message, wParam, lParam);
    }
    inline void Fire(UINT_PTR timer = 1)
    {
        Check(PostMessageW(window, WM_TIMER, timer, 0), "queue deterministic timer event");
    }
}

#undef CreateWindowW
#undef PostMessage
#define RegisterClassW WindowFixture::Register
#define UnregisterClassW WindowFixture::Unregister
#define CreateWindowW WindowFixture::Create
#define SetTimer WindowFixture::Timer
#define KillTimer WindowFixture::Cancel
#define PostMessage WindowFixture::Post
#include "../../MonitorEdgeTunnelLib/WindowMessageManager.cpp"
#undef RegisterClassW
#undef UnregisterClassW
#undef CreateWindowW
#undef SetTimer
#undef KillTimer
#undef PostMessage
