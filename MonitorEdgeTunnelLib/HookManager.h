#pragma once

#include <Windows.h>
#include <thread>
#include <atomic>
#include <functional>
#include <mutex>

/// <summary>
/// HookManager ��ҼҦ�
/// </summary>
class HookManager
{
public:
    /// <summary>
    /// ���o���
    /// </summary>
    /// <returns>���</returns>
    static HookManager& GetInstance();

    /// <summary>
    /// �}�lhook�C
    /// </summary>
    /// <returns>�O�_���\�A�p�G���椤�]�|��^���\</returns>
    bool Start();

    /// <summary>
    /// ����hook
    /// </summary>
    /// <returns>�O�_���\�A�p�G�D���椤�]�|��^���\</returns>
    bool Stop();
    
    /// <summary>
    /// �O�_�B�椤
    /// </summary>
    /// <returns>�O�_�B�椤</returns>
    bool IsRunning();

    /// <summary>
    /// �]�w��L���U��callback (system key)
    /// </summary>
    /// <param name="keyCode">Keycode</param>
    /// <param name="callback">Callback�C��^true�|�I�_�᭱��hook</param>
    /// <returns>hook�B�椤�|��^false</returns>
    bool SetKeycodeCallback(DWORD keyCode, const std::function<bool(DWORD)>& callback);

    /// <summary>
    /// �]�w�ƹ����ʮɪ�callback
    /// </summary>
    /// <param name="callback">Callback�C��^true�|�̾�POINT���ʷƹ��A�úI�_�᭱��hook</param>
    /// <returns>hook�B�椤�|��^false</returns>
    bool SetMouseMoveCallback(const std::function<bool(POINT&)>& callback);

private:
    /// <summary>
    /// �����
    /// </summary>
    std::thread m_thread;

    /// <summary>
    /// �W��Start�BStop�BIsRunning
    /// </summary>
    std::mutex m_mtx;

    /// <summary>
    /// �O�_����
    /// </summary>
    std::atomic_bool m_isRunning;

    /// <summary>
    /// �����ID
    /// </summary>
    DWORD m_threadID;

    /// <summary>
    /// �غc�l
    /// </summary>
    HookManager();

    /// <summary>
    /// �Ѻc�l
    /// </summary>
    ~HookManager();

    /// <summary>
    /// �������function
    /// </summary>
    void ThreadFunction();
};

