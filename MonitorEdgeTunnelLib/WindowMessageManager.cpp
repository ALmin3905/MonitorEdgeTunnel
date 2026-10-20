#include "pch.h"
#include "WindowMessageManager.h"
#include "Logger.h"

namespace
{
    /// <summary>
    /// 定義一個代表無效執行緒 ID 的常數。
    /// </summary>
    constexpr DWORD InvalidThreadID = 0;

	/// <summary>
	/// 定義一個常數，表示重新發送顯示變更事件的自訂 Windows 消息 ID。
	/// </summary>
	constexpr UINT WM_USER_REPOST_DISPLAYCHANGE_EVENT = WM_USER + 1;

    /// <summary>
    /// 定義一個常數，表示延遲處理螢幕變更事件的 Timer ID。
    /// </summary>
    constexpr WPARAM TIMER_DISPLAYCHANGE_DELAY = 1;

    /// <summary>
    /// 定義延遲處理螢幕變更事件的延遲時間（毫秒）。
    /// </summary>
    constexpr UINT DISPLAYCHANGE_DELAY_MS = 1000;
}

/// <summary>
/// 定義一個每個執行緒各自擁有的靜態 thread_local 指標變數 m_tlInstance，指向 WindowMessageManager 物件
/// </summary>
thread_local WindowMessageManager* WindowMessageManager::g_tlInstance = nullptr;

WindowMessageManager::WindowMessageManager() :
    m_isRunning(false),
    m_stopRequested(false),
    m_threadID(InvalidThreadID),
    m_hwnd(nullptr),
    m_mapWindowMessageProc({
        { WM_DISPLAYCHANGE, std::bind(&WindowMessageManager::OnDisplayChange, this, std::placeholders::_1, std::placeholders::_2, std::placeholders::_3) },
        { WM_TIMER,         std::bind(&WindowMessageManager::OnTimer,         this, std::placeholders::_1, std::placeholders::_2, std::placeholders::_3) },
		{ WM_USER_REPOST_DISPLAYCHANGE_EVENT, std::bind(&WindowMessageManager::OnRePostDisplayChangeEvent, this, std::placeholders::_1, std::placeholders::_2, std::placeholders::_3) }
    }),
    m_mapTimerMessageProc({
        { TIMER_DISPLAYCHANGE_DELAY, std::bind(&WindowMessageManager::OnTimerDisplayChangeDelay, this, std::placeholders::_1, std::placeholders::_2) }
    })
{

}

WindowMessageManager::~WindowMessageManager()
{
    Stop();

    // 不可讓仍使用此物件的工作執行緒超過物件生命週期。
    if (m_thread.joinable())
        m_thread.join();
}

bool WindowMessageManager::Start()
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
        m_thread = std::thread(&WindowMessageManager::ThreadFunction, this);
    }
    catch (const std::exception& e)
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, e.what());
        return false;
    }

    // Wait Thread Enable
    if (!m_singleWaitEvent.Wait(lockEvent))
    {
        // 保留執行緒所有權；完成初始化後也必須遵守停止請求。
        m_stopRequested = true;

        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "wait timeout");

        return false;
    }

    // Check Thread Failed
    if (!m_isRunning)
    {
        lockEvent.unlock();
        if (m_thread.joinable())
            m_thread.join();

        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "start failed");

        return false;
    }

    return true;
}

bool WindowMessageManager::Stop()
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

bool WindowMessageManager::IsRunning()
{
    std::lock_guard<std::mutex> lock(m_mtx);
    return m_isRunning;
}

bool WindowMessageManager::RePostDisplayChangeEvent()
{
    std::lock_guard<std::mutex> lock(m_mtx);

    if (!m_isRunning || m_stopRequested || !m_hwnd.load())
        return false;

    if (!PostMessage(m_hwnd.load(), WM_USER_REPOST_DISPLAYCHANGE_EVENT, 0, 0))
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "PostMessage failed, ErrorCode: " + std::to_string(GetLastError()));
		return false;
	}

	return true;
}

void WindowMessageManager::ThreadFunction()
{
    // get thread id
    m_threadID = GetCurrentThreadId();

    // 設定 thread local instance
    g_tlInstance = this;

    // create hidden window
    HWND hwnd = CreateHiddenWindow();
    m_hwnd = hwnd;
    if (!hwnd)
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Create Window failed, ErrorCode: " + std::to_string(GetLastError()));
        goto cleanup;
    }

    if (m_stopRequested)
        goto cleanup;

    m_isRunning = true;

    // 通知啟動功能的執行緒，Thread已啟動
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

        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

cleanup:
    m_hwnd = nullptr;

    // Destroy Window
    wchar_t className[128]{};
    if (hwnd)
        GetClassNameW(hwnd, className, 128);
    if (hwnd && !DestroyWindow(hwnd))
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Destroy Window failed, ErrorCode: " + std::to_string(GetLastError()));
    }

    if (className[0])
        UnregisterClassW(className, GetModuleHandleW(nullptr));

    // reset members
    m_isRunning = false;
    m_threadID = InvalidThreadID;
    g_tlInstance = nullptr;

    // 通知啟動功能的執行緒，Thread已結束
    m_singleWaitEvent.NotifyOne();
}

LRESULT WindowMessageManager::InstanceWindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    if (m_mapWindowMessageProc.count(msg))
        return m_mapWindowMessageProc.at(msg)(hwnd, wParam, lParam);

    return DefWindowProc(hwnd, msg, wParam, lParam);
}

LRESULT WindowMessageManager::OnDisplayChange(HWND hwnd, WPARAM wParam, LPARAM lParam)
{
    // 延遲處理螢幕變更事件 (避免可能螢幕資訊尚未準備完成，或是事件可能會race condition)
    KillTimer(hwnd, TIMER_DISPLAYCHANGE_DELAY);
    SetTimer(hwnd, TIMER_DISPLAYCHANGE_DELAY, DISPLAYCHANGE_DELAY_MS, NULL);

    return 0;
}

LRESULT WindowMessageManager::OnRePostDisplayChangeEvent(HWND hwnd, WPARAM wParam, LPARAM lParam)
{
    return OnDisplayChange(hwnd, wParam, lParam);
}

LRESULT WindowMessageManager::OnTimer(HWND hwnd, WPARAM wParam, LPARAM lParam)
{
    if (m_mapTimerMessageProc.count(wParam))
        return m_mapTimerMessageProc.at(wParam)(hwnd, lParam);

    return DefWindowProc(hwnd, WM_TIMER, wParam, lParam);
}

LRESULT WindowMessageManager::OnTimerDisplayChangeDelay(HWND hwnd, LPARAM lParam)
{
    KillTimer(hwnd, TIMER_DISPLAYCHANGE_DELAY);
    DisplayChangedDelegate.Invoke();

    return 0;
}

HWND WindowMessageManager::CreateHiddenWindow()
{
    std::wstring window_name = std::wstring(L"HiddenWindow_") + std::to_wstring(GetCurrentThreadId());
    std::wstring class_name = std::wstring(L"HiddenWindowClass_") + std::to_wstring(GetCurrentThreadId());

    WNDCLASSW wc = { 0 };
    wc.lpfnWndProc = WindowProc;
    wc.hInstance = GetModuleHandleW(NULL);
    wc.lpszClassName = class_name.c_str();

    if (RegisterClassW(&wc) == 0)
        return nullptr;

    HWND window = CreateWindowW(
        class_name.c_str(), window_name.c_str(),
        0, 0, 0, 0, 0,
        NULL, NULL, GetModuleHandleW(NULL), NULL
    );
    if (!window)
    {
        // A failed CreateWindow has no HWND for ThreadFunction's normal class cleanup.
        const DWORD error = GetLastError();
        UnregisterClassW(class_name.c_str(), wc.hInstance);
        SetLastError(error);
    }
    return window;
}

LRESULT WindowMessageManager::WindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    return g_tlInstance->InstanceWindowProc(hwnd, msg, wParam, lParam);
}
