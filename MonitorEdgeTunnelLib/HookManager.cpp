#include "pch.h"
#include "HookManager.h"
#include "Logger.h"

namespace
{
    /// <summary>
    /// 預設MouseMoveCallback，always return false
    /// </summary>
    constexpr auto MouseMoveCallbackDefault = [](POINT& pt) { return false; };

    /// <summary>
    /// 定義一個代表無效執行緒 ID 的常數。
    /// </summary>
    constexpr DWORD InvalidThreadID = 0;

    /// <summary>
    /// 定義一個自訂的 Windows 訊息常數，用於設定滑鼠移動回呼函式。
    /// </summary>
    constexpr UINT WM_SETMOUSEMOVECALLBACK = WM_USER + 1;

    /// <summary>
    /// 定義一個常數訊息代碼，用於設定系統按鍵回呼函式。
    /// </summary>
    constexpr UINT WM_SETSYSKEYCODECALLBACK = WM_USER + 2;
}

/// <summary>
/// 定義一個每個執行緒各自擁有的靜態 thread_local 指標變數 m_tlInstance，指向 HookManager 物件。
/// </summary>
thread_local HookManager* HookManager::g_tlInstance = nullptr;

HookManager::HookManager() :
    m_isRunning(false),
    m_stopRequested(false),
    m_threadID(InvalidThreadID),
    m_hookMouse(nullptr),
    m_hookKeyboard(nullptr),
    m_mouseMoveCallback(MouseMoveCallbackDefault)
{
    // 請在此加入自定義的訊息處理function
    m_customMessageProcMap = {
        { WM_SETMOUSEMOVECALLBACK, std::bind(&HookManager::OnSetMouseMoveCallback, this, std::placeholders::_1, std::placeholders::_2) },
        { WM_SETSYSKEYCODECALLBACK, std::bind(&HookManager::OnSetSysKeycodeCallback, this, std::placeholders::_1, std::placeholders::_2) }
    };
}

HookManager::~HookManager()
{
    Stop();

    // 不可讓仍使用此物件的工作執行緒超過物件生命週期。
    if (m_thread.joinable())
        m_thread.join();
}

bool HookManager::Start()
{
    std::lock_guard<std::mutex> lock(m_mtx);
    auto lockEvent = m_singleWaitEvent.GetLock();

    if (m_isRunning && !m_stopRequested)
        return true;

    // 逾時仍保留 joinable 的執行緒，禁止覆寫尚未退出的工作執行緒。
    if (m_thread.joinable())
    {
        if (WaitForSingleObject(m_thread.native_handle(), 0) != WAIT_OBJECT_0)
            return false;
        m_thread.join();
    }
    m_stopRequested = false;

    try
    {
        m_thread = std::thread(&HookManager::ThreadFunction, this);
    }
    catch (const std::exception& e)
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, e.what());
        return false;
    }

    // Wait Hook Enable
    if (!m_singleWaitEvent.Wait(lockEvent))
    {
        // 保留執行緒所有權；完成初始化後也必須遵守停止請求。
        m_stopRequested = true;

        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "wait timeout");

        return false;
    }

    // Check Hook Failed
    if (!m_isRunning)
    {
        lockEvent.unlock();
        if (m_thread.joinable())
            m_thread.join();

        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "hook thread failed");

        return false;
    }

    return true;
}

bool HookManager::Stop()
{
    std::lock_guard<std::mutex> lock(m_mtx);
    m_stopRequested = true;

    if (!m_thread.joinable())
        return true;

    // 回呼可要求停止，但必須由其他執行緒負責等待及解構。
    if (m_threadID.load() == GetCurrentThreadId())
        return false;

    // 訊息僅用於提早喚醒；即使送出失敗，訊息迴圈也會定期檢查停止請求。
    if (m_threadID.load())
        PostThreadMessage(m_threadID.load(), WM_QUIT, 0, 0);

    if (WaitForSingleObject(m_thread.native_handle(), 5000) != WAIT_OBJECT_0)
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "wait timeout; worker remains owned");
        return false;
    }

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

    if (m_stopRequested && m_thread.joinable())
        return false;

    if (m_isRunning)
    {
        auto lockEvent = m_singleWaitEvent.GetLock();

        m_tmpMouseMoveCallback = callback;

        if (!PostThreadMessage(m_threadID, WM_SETMOUSEMOVECALLBACK, 0, 0))
        {
            LOG_WITH_CONTEXT(Logger::LogLevel::Error, "PostThreadMessage failed");
            return false;
        }

        // 等待設定完成
        if (!m_singleWaitEvent.Wait(lockEvent))
        {
            LOG_WITH_CONTEXT(Logger::LogLevel::Error, "wait timeout");
            return false;
        }
    }
    else
    {
        if (callback)
            m_mouseMoveCallback = callback;
        else
            m_mouseMoveCallback = MouseMoveCallbackDefault;
    }

    return true;
}

bool HookManager::SetSysKeycodeCallback(DWORD keyCode, const SysKeycodeCallback& callback)
{
    std::lock_guard<std::mutex> lock(m_mtx);

    if (m_stopRequested && m_thread.joinable())
        return false;

    if (m_isRunning)
    {
        auto lockEvent = m_singleWaitEvent.GetLock();

        m_tmpSysKeycodeCallback = { keyCode, callback };

        if (!PostThreadMessage(m_threadID, WM_SETSYSKEYCODECALLBACK, 0, 0))
        {
            LOG_WITH_CONTEXT(Logger::LogLevel::Error, "PostThreadMessage failed");
            return false;
        }

        // 等待設定完成
        if (!m_singleWaitEvent.Wait(lockEvent))
        {
            LOG_WITH_CONTEXT(Logger::LogLevel::Error, "wait timeout");
            return false;
        }
    }
    else
    {
        m_sysKeycodeCallbackMap[keyCode] = callback;
    }

    return true;
}

void HookManager::ThreadFunction()
{
    // get thread id
    m_threadID = GetCurrentThreadId();

    // 必須先建立訊息佇列並設定實例，才能對外宣告已啟動。
    MSG initialMessage{};
    PeekMessage(&initialMessage, nullptr, WM_USER, WM_USER, PM_NOREMOVE);
    g_tlInstance = this;

    // hook mouse
    if (!(m_hookMouse = SetWindowsHookEx(WH_MOUSE_LL, HookMouseProc, NULL, NULL)))
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "mouse hook failed");
        goto cleanup;
    }

    // hook keyboard
    if (!(m_hookKeyboard = SetWindowsHookEx(WH_KEYBOARD_LL, HookKeyboardProc, NULL, NULL)))
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "keyboard hook failed");
        goto cleanup;
    }

    // 設定 thread local instance
    g_tlInstance = this;

    if (m_stopRequested)
        goto cleanup;

    m_isRunning = true;

    // 通知啟動功能的執行緒，Hook已啟動
    m_singleWaitEvent.NotifyOne();

    // 定期檢查停止請求，避免訊息無法送達時永久停留於 GetMessage。
    while (!m_stopRequested)
    {
        MSG msg{};
        if (!PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
        {
            MsgWaitForMultipleObjectsEx(0, nullptr, 100, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
            continue;
        }
        if (msg.message == WM_QUIT)
            break;

        // 自訂訊息處理
        if (m_customMessageProcMap.count(msg.message) && m_customMessageProcMap[msg.message])
            m_customMessageProcMap[msg.message](msg.wParam, msg.lParam);
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
    g_tlInstance = nullptr;

    // 通知啟動功能的執行緒，Hook已結束
    m_singleWaitEvent.NotifyOne();
}

void HookManager::OnSetMouseMoveCallback(WPARAM wParam, LPARAM lParam)
{
    if (m_tmpMouseMoveCallback)
        m_mouseMoveCallback = m_tmpMouseMoveCallback;
    else
        m_mouseMoveCallback = MouseMoveCallbackDefault;

    m_tmpMouseMoveCallback = nullptr;

    // 通知啟動功能的執行緒，callback已設定完成
    m_singleWaitEvent.NotifyOne();
}

void HookManager::OnSetSysKeycodeCallback(WPARAM wParam, LPARAM lParam)
{
    m_sysKeycodeCallbackMap[m_tmpSysKeycodeCallback.first] = m_tmpSysKeycodeCallback.second;

    m_tmpSysKeycodeCallback.first = 0;
    m_tmpSysKeycodeCallback.second = nullptr;

    // 通知啟動功能的執行緒，callback已設定完成
    m_singleWaitEvent.NotifyOne();
}

LRESULT WINAPI HookManager::HookMouseProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode >= 0 && lParam != NULL && wParam == WM_MOUSEMOVE)
    {
        POINT pt = ((LPMSLLHOOKSTRUCT)lParam)->pt;

        if (g_tlInstance->m_mouseMoveCallback(pt))
        {
            SetCursorPos(pt.x, pt.y);
            return -1;
        }
    }

    return CallNextHookEx(g_tlInstance->m_hookMouse, nCode, wParam, lParam);
}

LRESULT WINAPI HookManager::HookKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode >= 0 && lParam != NULL && wParam == WM_SYSKEYDOWN)
    {
        DWORD keyCode = ((PKBDLLHOOKSTRUCT)lParam)->vkCode;

        if (g_tlInstance->m_sysKeycodeCallbackMap.count(keyCode) &&
            g_tlInstance->m_sysKeycodeCallbackMap[keyCode] &&
            g_tlInstance->m_sysKeycodeCallbackMap[keyCode](keyCode))
        {
            return -1;
        }
    }

    return CallNextHookEx(g_tlInstance->m_hookKeyboard, nCode, wParam, lParam);
}
