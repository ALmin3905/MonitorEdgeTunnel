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
