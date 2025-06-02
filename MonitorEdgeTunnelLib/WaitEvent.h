#pragma once

#include <mutex>
#include <condition_variable>

/// <summary>
/// ���ݨƥ� (�u��ĳ�������������q�ϥΡA��h������L�k�O�Ҧ欰���T)
/// </summary>
class WaitEvent
{
public:
    /// <summary>
    /// ���o������
    /// </summary>
    using LockGuardType = std::unique_lock<std::mutex>;

    /// <param name="timeout">�W�ɬ��(�@��)</param>
    WaitEvent(long long timeout = 5000);

    ~WaitEvent();

    /// <summary>
    /// ���o��
    /// </summary>
    LockGuardType GetLock();

    /// <summary>
    /// ���� (�n���b�~���ϥ� GetLock �W��A�I�s�A�i�H�b�D�P�B����e����w�A�קK�D�P�B�y�{��������)
    /// <para>Exception : �ϥΫD GetLock ���o��������</para>
    /// </summary>
    /// <returns>��^���Ѫ�ܶW�ɵL�^��</returns>
    bool Wait(LockGuardType& lock);

    /// <summary>
    /// �����@����
    /// </summary>
    void NotifyOne();

private:
    std::chrono::milliseconds m_timeout;

    std::mutex m_mtxEvent;

    std::condition_variable m_cvEvent;

    bool m_predEvent;
};
