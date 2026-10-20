#pragma once

#include <Windows.h>
#include <atomic>
#include "HookManager.h"
#include "TestSupport.h"

namespace HookFixture
{
    // Capture hook procedures and execute them on the real worker's message queue.
    // No OS hook is installed and cursor changes are recorded rather than applied.
    inline std::atomic<HOOKPROC> mouse = nullptr, keyboard = nullptr;
    inline std::atomic<DWORD> worker = 0;
    inline std::atomic_int failKind = 0, installs = 0, removals = 0, warps = 0;
    inline std::atomic_bool failPost = false;
    inline POINT warped{};
    inline constexpr UINT InvokeMessage = WM_APP + 31;
    inline constexpr LRESULT NextResult = 777;

    struct Invocation
    {
        bool isMouse;
        int code;
        WPARAM message;
        bool nullData = false;
        MSLLHOOKSTRUCT mouseData{};
        KBDLLHOOKSTRUCT keyboardData{};
        LRESULT result = 0;
        HANDLE done = nullptr;
    };

    inline HHOOK Install(int kind, HOOKPROC callback, HINSTANCE, DWORD)
    {
        worker = GetCurrentThreadId();
        if (failKind == kind) return nullptr;
        if (kind == WH_MOUSE_LL) mouse = callback;
        else keyboard = callback;
        ++installs;
        return reinterpret_cast<HHOOK>(static_cast<INT_PTR>(kind));
    }
    inline BOOL Remove(HHOOK) { ++removals; return TRUE; }
    inline LRESULT Next(HHOOK, int, WPARAM, LPARAM) { return NextResult; }
    inline BOOL Warp(int x, int y) { warped = { x, y }; ++warps; return TRUE; }
    inline BOOL Post(DWORD thread, UINT message, WPARAM wParam, LPARAM lParam)
    {
        if (failPost.exchange(false)) return FALSE;
        return PostThreadMessageA(thread, message, wParam, lParam);
    }
    inline BOOL Peek(LPMSG message, HWND window, UINT minimum, UINT maximum, UINT remove)
    {
        const BOOL found = PeekMessageA(message, window, minimum, maximum, remove);
        if (found && remove == PM_REMOVE && message->message == InvokeMessage)
        {
            auto& command = *reinterpret_cast<Invocation*>(message->lParam);
            const auto data = command.nullData ? 0 : command.isMouse ?
                reinterpret_cast<LPARAM>(&command.mouseData) : reinterpret_cast<LPARAM>(&command.keyboardData);
            command.result = (command.isMouse ? mouse.load() : keyboard.load())(command.code, command.message, data);
            SetEvent(command.done);
            return FALSE;
        }
        return found;
    }
    inline LRESULT Execute(Invocation command)
    {
        command.done = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        Check(command.done != nullptr, "create invocation completion event");
        const bool posted = PostThreadMessageA(worker, InvokeMessage, 0, reinterpret_cast<LPARAM>(&command));
        const auto waited = posted ? WaitForSingleObject(command.done, 4000) : WAIT_FAILED;
        CloseHandle(command.done);
        Check(posted && waited == WAIT_OBJECT_0, "hook worker processes test invocation");
        return command.result;
    }
    inline LRESULT Mouse(int code = 0, WPARAM message = WM_MOUSEMOVE, bool nullData = false)
    {
        Invocation command{ true, code, message, nullData };
        command.mouseData.pt = { 12, 34 };
        return Execute(command);
    }
    inline LRESULT Key(DWORD key, int code = 0, WPARAM message = WM_SYSKEYDOWN, bool nullData = false)
    {
        Invocation command{ false, code, message, nullData };
        command.keyboardData.vkCode = key;
        return Execute(command);
    }
}

#undef SetWindowsHookEx
#undef PeekMessage
#undef PostThreadMessage
#define SetWindowsHookEx HookFixture::Install
#define UnhookWindowsHookEx HookFixture::Remove
#define CallNextHookEx HookFixture::Next
#define SetCursorPos HookFixture::Warp
#define PeekMessage HookFixture::Peek
#define PostThreadMessage HookFixture::Post
#include "../../MonitorEdgeTunnelLib/HookManager.cpp"
#undef SetWindowsHookEx
#undef UnhookWindowsHookEx
#undef CallNextHookEx
#undef SetCursorPos
#undef PeekMessage
#undef PostThreadMessage
