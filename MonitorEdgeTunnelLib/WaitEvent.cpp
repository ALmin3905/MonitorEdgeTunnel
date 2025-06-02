#include "pch.h"
#include "WaitEvent.h"

WaitEvent::WaitEvent(long long timeout) :
    m_timeout(timeout),
    m_predEvent(false)
{

}

WaitEvent::~WaitEvent()
{

}

WaitEvent::LockGuardType WaitEvent::GetLock()
{
    return LockGuardType(m_mtxEvent);
}

bool WaitEvent::Wait(LockGuardType& lock)
{
    if (lock.mutex() != &m_mtxEvent)
        throw std::logic_error("Wait Event using wrong mutex");

    m_predEvent = false;
    bool bRet = m_cvEvent.wait_for(lock, m_timeout, [&]() { return m_predEvent; });
    m_predEvent = false;
    return bRet;
}

void WaitEvent::NotifyOne()
{
    LockGuardType lock(m_mtxEvent);
    m_predEvent = true;
    m_cvEvent.notify_one();
}
