#pragma once

#include <Windows.h>
#include <thread>
#include "WaitEvent.h"
#include "MulticastDelegate.h"

/// <summary>
/// 建立隱藏視窗接收Window Message，並轉發訊息給訂閱者
/// </summary>
class WindowMessageManager
{
public:
    /// <summary>
    /// 啟動
    /// </summary>
    /// <returns>是否成功，如果執行中也會返回成功</returns>
    bool Start();

    /// <summary>
    /// 停止
    /// </summary>
    /// <returns>是否成功，如果非執行中也會返回成功</returns>
    bool Stop();

    /// <summary>
    /// 是否運行中
    /// </summary>
    bool IsRunning();

    /// <summary>
    /// 螢幕變更事件委託 (提供給外部訂閱事件) (WM_DISPLAYCHANGE)
    /// </summary>
    MulticastDelegate<> DisplayChangedDelegate;

public:
    /// <summary>
    /// 建構子
    /// </summary>
    WindowMessageManager();

    /// <summary>
    /// 解構子
    /// </summary>
    ~WindowMessageManager();

    // 禁止複製、移動
    WindowMessageManager(const WindowMessageManager&) = delete;
    WindowMessageManager(WindowMessageManager&&) = delete;
    WindowMessageManager& operator=(const WindowMessageManager&) = delete;
    WindowMessageManager& operator=(WindowMessageManager&&) = delete;

private:
    /// <summary>
    /// 執行緒的function
    /// </summary>
    void ThreadFunction();

    /// <summary>
    /// 實例WindowProc (靜態WindowProc應傳到此實例WindowProc)
    /// </summary>
    LRESULT InstanceWindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);

    /// <summary>
    /// 螢幕變更事件處理 (WM_DISPLAYCHANGE)
    /// </summary>
    LRESULT OnDisplayChange(WPARAM wParam, LPARAM lParam);

    /// <summary>
    /// 建立一個隱藏的視窗
    /// <para>綁定WndProc Function</para>
    /// </summary>
    /// <returns>隱藏視窗的HWND (請檢查是否為NULL)</returns>
    static HWND CreateHiddenWindow();

    /// <summary>
    /// 視窗訊息Function
    /// </summary>
    static LRESULT CALLBACK WindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);

    /// <summary>
    /// 執行緒
    /// </summary>
    std::thread m_thread;

    /// <summary>
    /// 單一等待事件
    /// </summary>
    SingleWaitEvent m_singleWaitEvent;

    /// <summary>
    /// 上鎖Start、Stop、IsRunning
    /// </summary>
    std::mutex m_mtx;

    /// <summary>
    /// 是否執行
    /// </summary>
    std::atomic_bool m_isRunning;

    /// <summary>
    /// 執行緒ID
    /// </summary>
    DWORD m_threadID;

    /// <summary>
    /// 轉送Window訊息的map
    /// </summary>
    std::unordered_map<UINT, std::function<LRESULT(WPARAM, LPARAM)>> m_mapMessageProc;

    /// <summary>
    /// 定義一個每個執行緒各自擁有的靜態 thread_local 指標變數 m_tlInstance，指向 WindowMessageManager 物件。
    /// </summary>
    static thread_local WindowMessageManager* g_tlInstance;
};
