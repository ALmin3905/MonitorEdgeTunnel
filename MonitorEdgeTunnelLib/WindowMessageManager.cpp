#include "pch.h"
#include "WindowMessageManager.h"
#include "Logger.h"

namespace
{
    /// <summary>
    /// 定義一個代表無效執行緒 ID 的常數。
    /// </summary>
    constexpr DWORD InvalidThreadID = 0;
}

/// <summary>
/// 定義一個每個執行緒各自擁有的靜態 thread_local 指標變數 m_tlInstance，指向 WindowMessageManager 物件
/// </summary>
thread_local WindowMessageManager* WindowMessageManager::g_tlInstance = nullptr;

WindowMessageManager::WindowMessageManager() :
    m_isRunning(false),
    m_threadID(InvalidThreadID)
{
    m_mapMessageProc = {
        { WM_DISPLAYCHANGE, std::bind(&WindowMessageManager::OnDisplayChange, this, std::placeholders::_1, std::placeholders::_2)}
    };
}

WindowMessageManager::~WindowMessageManager()
{
    Stop();
}

bool WindowMessageManager::Start()
{
    std::lock_guard<std::mutex> lock(m_mtx);
    auto lockEvent = m_singleWaitEvent.GetLock();

    if (m_isRunning)
        return true;

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
        // thread可能無回應了，只能detach
        if (m_thread.joinable())
            m_thread.detach();

        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "wait timeout");

        return false;
    }

    // Check Thread Failed
    if (!m_isRunning)
    {
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
    auto lockEvent = m_singleWaitEvent.GetLock();

    if (!m_isRunning)
        return true;

    // 結束 Thread
    if (!PostThreadMessage(m_threadID, WM_QUIT, 0, 0))
    {
        // 如果訊息傳遞失敗最保險的方式就是detach thread
        if (m_thread.joinable())
            m_thread.detach();

        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "PostThreadMessage failed");

        return false;
    }

    // Wait Thread End
    if (!m_singleWaitEvent.Wait(lockEvent))
    {
        // thread可能無回應了，只能detach
        if (m_thread.joinable())
            m_thread.detach();

        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "wait timeout");

        return false;
    }

    if (m_thread.joinable())
        m_thread.join();

    return true;
}

bool WindowMessageManager::IsRunning()
{
    std::lock_guard<std::mutex> lock(m_mtx);
    return m_isRunning;
}

void WindowMessageManager::ThreadFunction()
{
    // get thread id
    m_threadID = GetCurrentThreadId();

    // 設定 thread local instance
    g_tlInstance = this;

    // create hidden window
    HWND hwnd = CreateHiddenWindow();
    if (!hwnd)
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Create Window failed, ErrorCode: " + std::to_string(GetLastError()));
        goto cleanup;
    }

    m_isRunning = true;

    // 通知啟動功能的執行緒，Thread已啟動
    m_singleWaitEvent.NotifyOne();

    // getmessage (blocked)
    while (m_isRunning)
    {
        MSG msg = { 0 };
        if (BOOL bRet = GetMessage(&msg, NULL, 0, 0))
        {
            if (bRet == -1)
            {
                // 處理錯誤
                LOG_WITH_CONTEXT(Logger::LogLevel::Error, "GetMessage failed, ErrorCode: " + std::to_string(GetLastError()));
            }
            else
            {
                // do nothing......
            }
        }
        else
        {
            // 收到 WM_QUIT，結束Thread
            m_isRunning = false;
        }
    }

cleanup:
    // Destroy Window
    if (hwnd && !DestroyWindow(hwnd))
    {
        LOG_WITH_CONTEXT(Logger::LogLevel::Error, "Destroy Window failed, ErrorCode: " + std::to_string(GetLastError()));
    }

    // reset members
    m_isRunning = false;
    m_threadID = InvalidThreadID;
    g_tlInstance = nullptr;

    // 通知啟動功能的執行緒，Thread已結束
    m_singleWaitEvent.NotifyOne();
}

LRESULT WindowMessageManager::InstanceWindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    if (m_mapMessageProc.count(msg))
        return m_mapMessageProc.at(msg)(wParam, lParam);

    return DefWindowProc(hwnd, msg, wParam, lParam);
}

LRESULT WindowMessageManager::OnDisplayChange(WPARAM wParam, LPARAM lParam)
{
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
    RegisterClassW(&wc);

    return CreateWindowW(
        class_name.c_str(), window_name.c_str(),
        0, 0, 0, 0, 0,
        NULL, NULL, GetModuleHandleW(NULL), NULL
    );
}

LRESULT WindowMessageManager::WindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    return g_tlInstance->InstanceWindowProc(hwnd, msg, wParam, lParam);
}
