#include "HookManager.h"
#include <iostream>
#include <unordered_map>

namespace
{
    // mouse hook
    HHOOK g_hookMouse = NULL;

    // keyboard hook
    HHOOK g_hookKeyboard = NULL;

    // key對應的callback
    std::unordered_map<DWORD, std::function<bool(DWORD)>> g_keycodeCallback;

    // 滑鼠移動預設callback，不做任何事
    std::function<bool(POINT&)> g_mouseMoveCallbackDefault = [](POINT& pt) { return true; };

    // 滑鼠移動callback
    std::function<bool(POINT&)> g_mouseMoveCallback = g_mouseMoveCallbackDefault;

    // Mouse Hook Callback
    LRESULT WINAPI HookMouseCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0 && lParam != NULL && wParam == WM_MOUSEMOVE)
        {
            POINT pt = ((LPMSLLHOOKSTRUCT)lParam)->pt;

            if (g_mouseMoveCallback(pt))
            {
                SetCursorPos(pt.x, pt.y);
                return -1;
            }
        }

        return CallNextHookEx(g_hookMouse, nCode, wParam, lParam);
    }

    // Keyboard Hook Callback
    LRESULT WINAPI HookKeyboardCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0 && lParam != NULL && wParam == WM_SYSKEYDOWN)
        {
            DWORD keyCode = ((PKBDLLHOOKSTRUCT)lParam)->vkCode;

            if (g_keycodeCallback.count(keyCode))
            {
                if (g_keycodeCallback[keyCode](keyCode))
                    return -1;
            }
        }

        return CallNextHookEx(g_hookKeyboard, nCode, wParam, lParam);
    }
}

/*static*/ HookManager& HookManager::GetInstance()
{
    static HookManager instance;
    return instance;
}

HookManager::HookManager() : m_isRunning(false), m_threadID(-1)
{

}

HookManager::~HookManager()
{
    Stop();
}

bool HookManager::Start()
{
    if (m_isRunning)
        return true;

    m_isRunning = true;

    try
    {
        m_thread = std::thread(&HookManager::ThreadFunction, this);
    }
    catch (const std::exception& e)
    {
        std::cout << e.what() << std::endl;
        m_isRunning = false;
        return false;
    }

    return true;
}

bool HookManager::Stop()
{
    if (!m_isRunning)
        return true;

    m_isRunning = false;
    ::PostThreadMessage(m_threadID, WM_QUIT, 0, 0);

    try
    {
        m_thread.join();
    }
    catch (const std::exception& e)
    {
        std::cout << e.what() << std::endl;
        return false;
    }

    return true;
}

bool HookManager::IsRunning()
{
    return m_isRunning;
}

void HookManager::SetKeycodeCallback(DWORD keyCode, const std::function<bool(DWORD)>& callback)
{
    g_keycodeCallback[keyCode] = callback;
}

void HookManager::SetMouseMoveCallback(const std::function<bool(POINT&)>& callback)
{
    if (callback)
        g_mouseMoveCallback = callback;
    else
        g_mouseMoveCallback = g_mouseMoveCallbackDefault;
}

void HookManager::ThreadFunction()
{
    // get thread id
    m_threadID = GetCurrentThreadId();

    // hook mouse
    if (!(g_hookMouse = SetWindowsHookEx(WH_MOUSE_LL, HookMouseCallback, NULL, NULL)))
    {
        std::cout << "hook mouse failed" << std::endl;
        goto cleanup;
    }

    // hook keyboard
    if (!(g_hookKeyboard = SetWindowsHookEx(WH_KEYBOARD_LL, HookKeyboardCallback, NULL, NULL)))
    {
        std::cout << "hook keyboard failed" << std::endl;
        goto cleanup;
    }

    // getmessage (blocked)
    {
        MSG msg = { 0 };
        while (m_isRunning)
        {
            if (GetMessage(&msg, NULL, 0, 0))
            {
                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }
        }
    }

cleanup:
    m_isRunning = false;
    m_threadID = -1;
    if (g_hookMouse)
        UnhookWindowsHookEx(g_hookMouse);
    if (g_hookKeyboard)
        UnhookWindowsHookEx(g_hookKeyboard);
}