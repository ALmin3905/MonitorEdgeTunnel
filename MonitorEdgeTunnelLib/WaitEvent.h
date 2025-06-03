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

    /// <param name="timeout">超時秒數(毫秒)</param>
    SingleWaitEvent(long long timeout = 5000);

    ~SingleWaitEvent();

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

private:
    std::chrono::milliseconds m_timeout;

    std::mutex m_mtxEvent;

    std::condition_variable m_cvEvent;

    bool m_predEvent;

    int m_waitCount;
};

/// <summary>
/// 多執行緒等待事件
/// </summary>
class MultiWaitEvent
{
public:
    /// <summary>
    /// 取得鎖類型
    /// </summary>
    using LockGuardType = std::unique_lock<std::mutex>;

    MultiWaitEvent(long long timeout = 5000);

    ~MultiWaitEvent();

    /// <summary>
    /// 取得鎖
    /// </summary>
    LockGuardType GetLock();

    /// <summary>
    /// 等待 (要先在外部使用 GetLock 上鎖再呼叫，可以在非同步執行前先鎖定，避免非同步流程先執行喚醒)
    /// <para>Exception : 1. 使用非 GetLock 取得的互斥鎖。</para>
    /// </summary>
    /// <returns>返回失敗表示超時無回應</returns>
    bool Wait(LockGuardType& lock);

    /// <summary>
    /// 喚醒全部等待 (直到所有執行緒都喚醒才會重置事件，在這期間加入等待的執行緒都會直接返回true)
    /// </summary>
    void NotifyAll();

private:
    std::chrono::milliseconds m_timeout;

    std::mutex m_mtxEvent;

    std::condition_variable m_cvEvent;

    bool m_predEvent;

    int m_waitCount;
};
