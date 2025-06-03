#pragma once

#include <mutex>
#include <condition_variable>

/// <summary>
/// ��@��������ݨƥ�
/// <para>�ȯ�b�����������ϥ�</para>
/// </summary>
class SingleWaitEvent
{
public:
    /// <summary>
    /// ���o������
    /// </summary>
    using LockGuardType = std::unique_lock<std::mutex>;

    /// <param name="timeout">�W�ɬ��(�@��)</param>
    SingleWaitEvent(long long timeout = 5000);

    ~SingleWaitEvent();

    /// <summary>
    /// ���o��
    /// </summary>
    LockGuardType GetLock();

    /// <summary>
    /// ���� (�n���b�~���ϥ� GetLock �W��A�I�s�A�i�H�b�D�P�B����e����w�A�קK�D�P�B�y�{��������)
    /// <para>Exception : 1. �ϥΫD GetLock ���o��������C 2. ���ݪ�������W�L1�ӡC</para>
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

    int m_waitCount;
};

/// <summary>
/// �h��������ݨƥ�
/// </summary>
class MultiWaitEvent
{
public:
    /// <summary>
    /// ���o������
    /// </summary>
    using LockGuardType = std::unique_lock<std::mutex>;

    MultiWaitEvent(long long timeout = 5000);

    ~MultiWaitEvent();

    /// <summary>
    /// ���o��
    /// </summary>
    LockGuardType GetLock();

    /// <summary>
    /// ���� (�n���b�~���ϥ� GetLock �W��A�I�s�A�i�H�b�D�P�B����e����w�A�קK�D�P�B�y�{��������)
    /// <para>Exception : 1. �ϥΫD GetLock ���o��������C</para>
    /// </summary>
    /// <returns>��^���Ѫ�ܶW�ɵL�^��</returns>
    bool Wait(LockGuardType& lock);

    /// <summary>
    /// ����������� (����Ҧ������������~�|���m�ƥ�A�b�o�����[�J���ݪ���������|������^true)
    /// </summary>
    void NotifyAll();

private:
    std::chrono::milliseconds m_timeout;

    std::mutex m_mtxEvent;

    std::condition_variable m_cvEvent;

    bool m_predEvent;

    int m_waitCount;
};
