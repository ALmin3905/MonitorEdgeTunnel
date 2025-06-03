#pragma once

#include <Windows.h>
#include <thread>
#include <atomic>
#include <functional>
#include <mutex>
#include <map>
#include "WaitEvent.h"

/// <summary>
/// HookManager 單例模式
/// </summary>
class HookManager
{
public:
    /// <summary>
    /// 滑鼠移動事件function
    /// <param name="POINT">滑鼠的點位，可修改位置</param>
    /// <returns>回傳true會依據POINT修改滑鼠位置，並截斷後續Hook流程</returns>
    /// </summary>
    using MouseMoveCallback = std::function<bool(POINT&)>;

    /// <summary>
    /// 鍵盤事件function
    /// <param name="DWORD">Keycode</param>
    /// <returns>回傳true會截斷後續Hook流程</returns>
    /// </summary>
    using SysKeycodeCallback = std::function<bool(DWORD)>;

    /// <summary>
    /// 取得實例
    /// </summary>
    static HookManager& GetInstance();

    /// <summary>
    /// 開始hook。
    /// </summary>
    /// <returns>是否成功，如果執行中也會返回成功</returns>
    bool Start();

    /// <summary>
    /// 停止hook
    /// </summary>
    /// <returns>是否成功，如果非執行中也會返回成功</returns>
    bool Stop();
    
    /// <summary>
    /// 是否運行中
    /// </summary>
    /// <returns>是否運行中</returns>
    bool IsRunning();

    /// <summary>
    /// 設定滑鼠移動時的callback
    /// </summary>
    /// <param name="callback">Callback。返回true會依據POINT移動滑鼠，並截斷後面的hook</param>
    /// <returns>是否設定成功 (運行中可能會失敗)</returns>
    bool SetMouseMoveCallback(const MouseMoveCallback& callback);

    /// <summary>
    /// 設定鍵盤按下的callback (system key)
    /// </summary>
    /// <param name="keyCode">Keycode</param>
    /// <param name="callback">Callback。返回true會截斷後面的hook</param>
    /// <returns>是否設定成功 (運行中可能會失敗)</returns>
    bool SetSysKeycodeCallback(DWORD keyCode, const SysKeycodeCallback& callback);

private:
    /// <summary>
    /// 建構子
    /// </summary>
    HookManager();

    /// <summary>
    /// 解構子
    /// </summary>
    ~HookManager();

    /// <summary>
    /// Hook執行緒的function
    /// </summary>
    void ThreadFunction();

    /// <summary>
    /// 當Hook執行中時，會透過Message告訴Hook Thread進行設定SetMouseMoveCallback
    /// </summary>
    /// <param name="wParam">無用</param>
    /// <param name="lParam">無用</param>
    void OnSetMouseMoveCallback(WPARAM wParam, LPARAM lParam);

    /// <summary>
    /// 當Hook執行中時，會透過Message告訴Hook Thread進行設定SetSysKeycodeCallback
    /// </summary>
    /// <param name="wParam">無用</param>
    /// <param name="lParam">無用</param>
    void OnSetSysKeycodeCallback(WPARAM wParam, LPARAM lParam);

    /// <summary>
    /// Hook滑鼠事件處理函式
    /// </summary>
    static LRESULT WINAPI HookMouseProc(int nCode, WPARAM wParam, LPARAM lParam);

    /// <summary>
    /// Hook鍵盤事件處理函式
    /// </summary>
    static LRESULT WINAPI HookKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);

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
    /// mouse hook
    /// </summary>
    HHOOK m_hookMouse;

    /// <summary>
    /// keyboard hook
    /// </summary>
    HHOOK m_hookKeyboard;

    /// <summary>
    /// mouse move callback
    /// </summary>
    MouseMoveCallback m_mouseMoveCallback;

    /// <summary>
    /// system keycode callback map
    /// </summary>
    std::map<DWORD, SysKeycodeCallback> m_sysKeycodeCallbackMap;

    /// <summary>
    /// 自訂訊息處理函式類型
    /// </summary>
    using CostomMessageProc = std::function<void(WPARAM, LPARAM)>;

    /// <summary>
    /// 儲存自訂訊息處理函式的對應關係的Map，會在Hook執行中使用
    /// </summary>
    std::map<UINT, CostomMessageProc> m_customMessageProcMap;

    /// <summary>
    /// 暫存的滑鼠移動callback，用於Hook執行中設定
    /// </summary>
    MouseMoveCallback m_tmpMouseMoveCallback;

    /// <summary>
    /// 用於暫存鍵盤callback，包含keycode和callback
    /// </summary>
    std::pair<DWORD, SysKeycodeCallback> m_tmpSysKeycodeCallback;

    /// <summary>
    /// 定義一個每個執行緒各自擁有的靜態 thread_local 指標變數 m_tlInstance，指向 HookManager 物件。
    /// </summary>
    static thread_local HookManager* g_tlInstance;
};

