#pragma once

#include <mutex>
#include <condition_variable>

/// <summary>
/// 等待事件 (只建議兩執行緒之間溝通使用，更多執行緒無法保證行為正確)
/// </summary>
class WaitEvent
{
public:
    /// <summary>
    /// 取得鎖類型
    /// </summary>
    using LockGuardType = std::unique_lock<std::mutex>;

    /// <param name="timeout">超時秒數(毫秒)</param>
    WaitEvent(long long timeout = 5000);

    ~WaitEvent();

    /// <summary>
    /// 取得鎖
    /// </summary>
    LockGuardType GetLock();

    /// <summary>
    /// 等待 (要先在外部使用 GetLock 上鎖再呼叫，可以在非同步執行前先鎖定，避免非同步流程先執行喚醒)
    /// <para>Exception : 使用非 GetLock 取得的互斥鎖</para>
    /// </summary>
    /// <returns>返回失敗表示超時無回應</returns>
    bool Wait(LockGuardType& lock);

    /// <summary>
    /// 喚醒單一等待
    /// </summary>
    void NotifyOne();

private:
    std::chrono::milliseconds m_timeout;

    std::mutex m_mtxEvent;

    std::condition_variable m_cvEvent;

    bool m_predEvent;
};
