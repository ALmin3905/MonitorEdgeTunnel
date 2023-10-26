#pragma once

#include <Windows.h>
#include <thread>
#include <atomic>
#include <functional>

/// <summary>
/// HookManager 單例模式
/// </summary>
class HookManager
{
public:
    /// <summary>
    /// 取得實例
    /// </summary>
    /// <returns>實例</returns>
    static HookManager& GetInstance();

    /// <summary>
    /// 開始hook
    /// </summary>
    /// <returns>是否成功</returns>
    bool Start();

    /// <summary>
    /// 停止hook
    /// </summary>
    /// <returns>是否成功</returns>
    bool Stop();
    
    /// <summary>
    /// 是否運行中
    /// </summary>
    /// <returns>是否運行中</returns>
    bool IsRunning();

    /// <summary>
    /// 設定鍵盤按下的callback (system key)
    /// </summary>
    /// <param name="keyCode">Keycode</param>
    /// <param name="callback">Callback</param>
    void SetKeycodeCallback(DWORD keyCode, const std::function<bool(DWORD)>& callback);

    /// <summary>
    /// 設定滑鼠移動時的callback
    /// </summary>
    /// <param name="callback">Callback</param>
    void SetMouseMoveCallback(const std::function<bool(POINT&)>& callback);

private:
    /// <summary>
    /// 執行緒
    /// </summary>
    std::thread m_thread;

    /// <summary>
    /// 是否執行
    /// </summary>
    std::atomic_bool m_isRunning;

    /// <summary>
    /// 執行緒ID
    /// </summary>
    DWORD m_threadID;

    /// <summary>
    /// 建構子
    /// </summary>
    HookManager();

    /// <summary>
    /// 解構子
    /// </summary>
    ~HookManager();

    /// <summary>
    /// 執行緒的function
    /// </summary>
    void ThreadFunction();
};

