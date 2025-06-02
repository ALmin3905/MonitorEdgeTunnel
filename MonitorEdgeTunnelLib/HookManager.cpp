#include "pch.h"
#include "HookManager.h"
#include <iostream>

namespace
{
    /// <summary>
    /// �w�]MouseMoveCallback�Aalways return false
    /// </summary>
    constexpr auto MouseMoveCallbackDefault = [](POINT& pt) { return false; };

    /// <summary>
    /// �w�q�@�ӥN��L�İ���� ID ���`�ơC
    /// </summary>
    constexpr DWORD InvalidThreadID = -1;

    /// <summary>
    /// �w�q�@�Ӧۭq�� Windows �T���`�ơA�Ω�]�w�ƹ����ʦ^�I�禡�C
    /// </summary>
    constexpr UINT WM_SETMOUSEMOVECALLBACK = WM_USER + 1;

    /// <summary>
    /// �w�q�@�ӱ`�ưT���N�X�A�Ω�]�w�t�Ϋ���^�I�禡�C
    /// </summary>
    constexpr UINT WM_SETSYSKEYCODECALLBACK = WM_USER + 2;
}

/*static*/
HookManager& HookManager::GetInstance()
{
    static HookManager instance;
    return instance;
}

HookManager::HookManager() :
    m_isRunning(false),
    m_threadID(InvalidThreadID),
    m_hookMouse(nullptr),
    m_hookKeyboard(nullptr),
    m_mouseMoveCallback(MouseMoveCallbackDefault)
{
    m_customMessageProcMap = {
        { WM_SETMOUSEMOVECALLBACK, std::bind(&HookManager::OnSetMouseMoveCallback, this, std::placeholders::_1, std::placeholders::_2) },
        { WM_SETSYSKEYCODECALLBACK, std::bind(&HookManager::OnSetSysKeycodeCallback, this, std::placeholders::_1, std::placeholders::_2) }
    };
}

HookManager::~HookManager()
{
    Stop();
}

bool HookManager::Start()
{
    std::lock_guard<std::mutex> lock(m_mtx);
    auto lockEvent = m_we.GetLock();

    if (m_isRunning)
        return true;

    try
    {
        m_thread = std::thread(&HookManager::ThreadFunction, this);
    }
    catch (const std::exception& e)
    {
        std::cout << e.what() << std::endl;
        return false;
    }

    // Wait Hook Enable
    if (!m_we.Wait(lockEvent))
    {
        // thread�i��L�^���F�A�u��detach
        if (m_thread.joinable())
            m_thread.detach();

        return false;
    }

    // Check Hook Failed
    if (!m_isRunning)
    {
        if (m_thread.joinable())
            m_thread.join();

        return false;
    }

    return true;
}

bool HookManager::Stop()
{
    std::lock_guard<std::mutex> lock(m_mtx);
    auto lockEvent = m_we.GetLock();

    if (!m_isRunning)
        return true;

    // PostThreadMessage ��� GetMessage ����
    if (!PostThreadMessage(m_threadID, WM_QUIT, 0, 0))
    {
        // �p�G�T���ǻ����ѳ̫O�I���覡�N�Odetach thread
        if (m_thread.joinable())
            m_thread.detach();

        return false;
    }

    // Wait Hook End
    if (!m_we.Wait(lockEvent))
    {
        // thread�i��L�^���F�A�u��detach
        if (m_thread.joinable())
            m_thread.detach();

        return false;
    }

    if (m_thread.joinable())
        m_thread.join();

    return true;
}

bool HookManager::IsRunning()
{
    std::lock_guard<std::mutex> lock(m_mtx);
    return m_isRunning;
}

bool HookManager::SetMouseMoveCallback(const MouseMoveCallback& callback)
{
    std::lock_guard<std::mutex> lock(m_mtx);

    if (m_isRunning)
    {
        auto lockEvent = m_we.GetLock();

        m_tmpMouseMoveCallback = callback;

        if (!PostThreadMessage(m_threadID, WM_SETMOUSEMOVECALLBACK, 0, 0))
        {
            return false;
        }

        // ���ݦ^�I�]�w����
        if (!m_we.Wait(lockEvent))
        {
            return false;
        }

        return true;
    }
    else
    {
        if (callback)
            m_mouseMoveCallback = callback;
        else
            m_mouseMoveCallback = MouseMoveCallbackDefault;

        return true;
    }
}

bool HookManager::SetSysKeycodeCallback(DWORD keyCode, const SysKeycodeCallback& callback)
{
    std::lock_guard<std::mutex> lock(m_mtx);

    if (m_isRunning)
    {
        auto lockEvent = m_we.GetLock();

        m_tmpSysKeycodeCallback = { keyCode, callback };

        if (!PostThreadMessage(m_threadID, WM_SETSYSKEYCODECALLBACK, 0, 0))
        {
            return false;
        }

        // ���ݦ^�I�]�w����
        if (!m_we.Wait(lockEvent))
        {
            return false;
        }

        return true;
    }
    else
    {
        m_sysKeycodeCallbackMap[keyCode] = callback;
        return true;
    }
}

void HookManager::ThreadFunction()
{
    // get thread id
    m_threadID = GetCurrentThreadId();

    // hook mouse
    if (!(m_hookMouse = SetWindowsHookEx(WH_MOUSE_LL, HookMouseProc, NULL, NULL)))
    {
        std::cout << "hook mouse failed" << std::endl;
        goto cleanup;
    }

    // hook keyboard
    if (!(m_hookKeyboard = SetWindowsHookEx(WH_KEYBOARD_LL, HookKeyboardProc, NULL, NULL)))
    {
        std::cout << "hook keyboard failed" << std::endl;
        goto cleanup;
    }

    m_isRunning = true;

    // �q���Ұʥ\�઺������AHook�w�Ұ�
    m_we.NotifyOne();

    // getmessage (blocked)
    while (m_isRunning)
    {
        MSG msg = { 0 };
        if (BOOL bRet = GetMessage(&msg, NULL, 0, 0))
        {
            if (bRet == -1)
            {
                // �B�z���~
                std::cout << "GetMessage failed: " << GetLastError() << std::endl;
            }
            else
            {
                // �ۭq�T���B�z
                if (m_customMessageProcMap.count(msg.message) && m_customMessageProcMap[msg.message])
                {
                    m_customMessageProcMap[msg.message](msg.wParam, msg.lParam);
                }
            }
        }
        else
        {
            // ���� WM_QUIT�A����Hook
            m_isRunning = false;
        }
    }

cleanup:
    // unhook
    if (m_hookMouse)
        UnhookWindowsHookEx(m_hookMouse);
    if (m_hookKeyboard)
        UnhookWindowsHookEx(m_hookKeyboard);

    // reset members
    m_hookMouse = nullptr;
    m_hookKeyboard = nullptr;
    m_isRunning = false;
    m_threadID = InvalidThreadID;

    // �q���Ұʥ\�઺������AHook�w����
    m_we.NotifyOne();
}

void HookManager::OnSetMouseMoveCallback(WPARAM wParam, LPARAM lParam)
{
    if (m_tmpMouseMoveCallback)
        m_mouseMoveCallback = m_tmpMouseMoveCallback;
    else
        m_mouseMoveCallback = MouseMoveCallbackDefault;

    // �q���Ұʥ\�઺������Acallback�w�]�w����
    m_we.NotifyOne();
}

void HookManager::OnSetSysKeycodeCallback(WPARAM wParam, LPARAM lParam)
{
    m_sysKeycodeCallbackMap[m_tmpSysKeycodeCallback.first] = m_tmpSysKeycodeCallback.second;

    // �q���Ұʥ\�઺������Acallback�w�]�w����
    m_we.NotifyOne();
}

/*static*/
LRESULT WINAPI HookManager::HookMouseProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    HookManager& instance = HookManager::GetInstance();

    if (nCode >= 0 && lParam != NULL && wParam == WM_MOUSEMOVE)
    {
        POINT pt = ((LPMSLLHOOKSTRUCT)lParam)->pt;

        if (instance.m_mouseMoveCallback(pt))
        {
            SetCursorPos(pt.x, pt.y);
            return -1;
        }
    }

    return CallNextHookEx(instance.m_hookMouse, nCode, wParam, lParam);
}

/*static*/
LRESULT WINAPI HookManager::HookKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    HookManager& instance = HookManager::GetInstance();

    if (nCode >= 0 && lParam != NULL && wParam == WM_SYSKEYDOWN)
    {
        DWORD keyCode = ((PKBDLLHOOKSTRUCT)lParam)->vkCode;

        if (instance.m_sysKeycodeCallbackMap.count(keyCode))
        {
            if (instance.m_sysKeycodeCallbackMap[keyCode] && instance.m_sysKeycodeCallbackMap[keyCode](keyCode))
                return -1;
        }
    }

    return CallNextHookEx(instance.m_hookKeyboard, nCode, wParam, lParam);
}
