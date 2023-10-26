#pragma once

#include <Windows.h>
#include <thread>
#include <atomic>
#include <functional>

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
    /// �}�lhook
    /// </summary>
    /// <returns>�O�_���\</returns>
    bool Start();

    /// <summary>
    /// ����hook
    /// </summary>
    /// <returns>�O�_���\</returns>
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
    /// <param name="callback">Callback</param>
    void SetKeycodeCallback(DWORD keyCode, const std::function<bool(DWORD)>& callback);

    /// <summary>
    /// �]�w�ƹ����ʮɪ�callback
    /// </summary>
    /// <param name="callback">Callback</param>
    void SetMouseMoveCallback(const std::function<bool(POINT&)>& callback);

private:
    /// <summary>
    /// �����
    /// </summary>
    std::thread m_thread;

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

