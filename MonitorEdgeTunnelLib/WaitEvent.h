#pragma once

#include <mutex>
#include <condition_variable>

/// <summary>
/// 單一執行緒等待事件
/// <para>僅能在兩執行緒之間使用</para>
/// </summary>
class SingleWaitEvent
{
public:
    /// <summary>
    /// 取得鎖類型
    /// </summary>
    using LockGuardType = std::unique_lock<std::mutex>;

    /// <summary>
    /// 取得鎖
    /// </summary>
    LockGuardType GetLock();

    /// <summary>
    /// 等待 (要先在外部使用 GetLock 上鎖再呼叫，可以在非同步執行前先鎖定，避免非同步流程先執行喚醒)
    /// <para>Exception : 1. 使用非 GetLock 取得的互斥鎖。 2. 等待的執行緒超過1個。</para>
    /// </summary>
    /// <returns>返回失敗表示超時無回應</returns>
    bool Wait(LockGuardType& lock);

    /// <summary>
    /// 喚醒單一等待
    /// </summary>
    void NotifyOne();

public:
    /// <param name="timeout">超時秒數(毫秒)</param>
    SingleWaitEvent(long long timeout = 5000);

    ~SingleWaitEvent();

    // 禁止複製、移動
    SingleWaitEvent(const SingleWaitEvent&) = delete;
    SingleWaitEvent(SingleWaitEvent&&) = delete;
    SingleWaitEvent& operator=(const SingleWaitEvent&) = delete;
    SingleWaitEvent& operator=(SingleWaitEvent&&) = delete;

private:
    std::chrono::milliseconds m_timeout;

    std::mutex m_mtxEvent;

    std::condition_variable m_cvEvent;

    bool m_predEvent;

    int m_waitCount;
};
