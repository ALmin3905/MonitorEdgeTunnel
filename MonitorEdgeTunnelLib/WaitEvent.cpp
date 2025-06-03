#include "pch.h"
#include "WaitEvent.h"

SingleWaitEvent::SingleWaitEvent(long long timeout) :
    m_timeout(timeout),
    m_predEvent(true),
    m_waitCount(0)
{

}

SingleWaitEvent::~SingleWaitEvent()
{

}

SingleWaitEvent::LockGuardType SingleWaitEvent::GetLock()
{
    return LockGuardType(m_mtxEvent);
}

bool SingleWaitEvent::Wait(LockGuardType& lock)
{
    if (lock.mutex() != &m_mtxEvent)
        throw std::logic_error("Wait Event using wrong mutex");

    if (m_waitCount)
        throw std::logic_error("Wait Event more than 1");

    m_predEvent = false;
    ++m_waitCount;
    bool bRet = m_cvEvent.wait_for(lock, m_timeout, [&]() { return m_predEvent; });
    --m_waitCount;

    return bRet;
}

void SingleWaitEvent::NotifyOne()
{
    LockGuardType lock(m_mtxEvent);
    m_predEvent = true;
    m_cvEvent.notify_one();
}

///////////////////////////////////////////////////////////////////////////////////////////////////////////

MultiWaitEvent::MultiWaitEvent(long long timeout) :
    m_timeout(timeout),
    m_predEvent(true),
    m_waitCount(0)
{

}

MultiWaitEvent::~MultiWaitEvent()
{

}

MultiWaitEvent::LockGuardType MultiWaitEvent::GetLock()
{
    return LockGuardType(m_mtxEvent);
}

bool MultiWaitEvent::Wait(LockGuardType& lock)
{
    if (lock.mutex() != &m_mtxEvent)
        throw std::logic_error("Wait Event using wrong mutex");

    // 如果沒有執行緒，重置事件狀態
    if (!m_waitCount)
        m_predEvent = false;

    ++m_waitCount;
    bool bRet = m_cvEvent.wait_for(lock, m_timeout, [&]() { return m_predEvent; });
    --m_waitCount;

    return bRet;
}

void MultiWaitEvent::NotifyAll()
{
    LockGuardType lock(m_mtxEvent);
    m_predEvent = true;
    m_cvEvent.notify_all();
}
