#pragma once

#include <Windows.h>
#include <thread>
#include <atomic>
#include <functional>
#include <mutex>
#include <map>
#include "WaitEvent.h"

/// <summary>
/// HookManager ��ҼҦ�
/// </summary>
class HookManager
{
public:
    /// <summary>
    /// �ƹ����ʨƥ�function
    /// <param name="POINT">�ƹ����I��A�i�ק��m</param>
    /// <returns>�^��true�|�̾�POINT�ק�ƹ���m�A�úI�_����Hook�y�{</returns>
    /// </summary>
    using MouseMoveCallback = std::function<bool(POINT&)>;

    /// <summary>
    /// ��L�ƥ�function
    /// <param name="DWORD">Keycode</param>
    /// <returns>�^��true�|�I�_����Hook�y�{</returns>
    /// </summary>
    using SysKeycodeCallback = std::function<bool(DWORD)>;

    /// <summary>
    /// ���o���
    /// </summary>
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
    /// �]�w�ƹ����ʮɪ�callback
    /// </summary>
    /// <param name="callback">Callback�C��^true�|�̾�POINT���ʷƹ��A�úI�_�᭱��hook</param>
    /// <returns>�O�_�]�w���\ (�B�椤�i��|����)</returns>
    bool SetMouseMoveCallback(const MouseMoveCallback& callback);

    /// <summary>
    /// �]�w��L���U��callback (system key)
    /// </summary>
    /// <param name="keyCode">Keycode</param>
    /// <param name="callback">Callback�C��^true�|�I�_�᭱��hook</param>
    /// <returns>�O�_�]�w���\ (�B�椤�i��|����)</returns>
    bool SetSysKeycodeCallback(DWORD keyCode, const SysKeycodeCallback& callback);

private:
    /// <summary>
    /// �غc�l
    /// </summary>
    HookManager();

    /// <summary>
    /// �Ѻc�l
    /// </summary>
    ~HookManager();

    /// <summary>
    /// Hook�������function
    /// </summary>
    void ThreadFunction();

    /// <summary>
    /// ��Hook���椤�ɡA�|�z�LMessage�i�DHook Thread�i��]�wSetMouseMoveCallback
    /// </summary>
    /// <param name="wParam">�L��</param>
    /// <param name="lParam">�L��</param>
    void OnSetMouseMoveCallback(WPARAM wParam, LPARAM lParam);

    /// <summary>
    /// ��Hook���椤�ɡA�|�z�LMessage�i�DHook Thread�i��]�wSetSysKeycodeCallback
    /// </summary>
    /// <param name="wParam">�L��</param>
    /// <param name="lParam">�L��</param>
    void OnSetSysKeycodeCallback(WPARAM wParam, LPARAM lParam);

    /// <summary>
    /// Hook�ƹ��ƥ�B�z�禡
    /// </summary>
    static LRESULT WINAPI HookMouseProc(int nCode, WPARAM wParam, LPARAM lParam);

    /// <summary>
    /// Hook��L�ƥ�B�z�禡
    /// </summary>
    static LRESULT WINAPI HookKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);

    /// <summary>
    /// �����
    /// </summary>
    std::thread m_thread;

    /// <summary>
    /// ��@���ݨƥ�
    /// </summary>
    SingleWaitEvent m_singleWaitEvent;

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
    /// mouse hook
    /// </summary>
    HHOOK m_hookMouse;

    /// <summary>
    /// keyboard hook
    /// </summary>
    HHOOK m_hookKeyboard;

    /// <summary>
    /// mouse move callback
    /// </summary>
    MouseMoveCallback m_mouseMoveCallback;

    /// <summary>
    /// system keycode callback map
    /// </summary>
    std::map<DWORD, SysKeycodeCallback> m_sysKeycodeCallbackMap;

    /// <summary>
    /// �ۭq�T���B�z�禡����
    /// </summary>
    using CostomMessageProc = std::function<void(WPARAM, LPARAM)>;

    /// <summary>
    /// �x�s�ۭq�T���B�z�禡���������Y��Map�A�|�bHook���椤�ϥ�
    /// </summary>
    std::map<UINT, CostomMessageProc> m_customMessageProcMap;

    /// <summary>
    /// �Ȧs���ƹ�����callback�A�Ω�Hook���椤�]�w
    /// </summary>
    MouseMoveCallback m_tmpMouseMoveCallback;

    /// <summary>
    /// �Ω�Ȧs��Lcallback�A�]�tkeycode�Mcallback
    /// </summary>
    std::pair<DWORD, SysKeycodeCallback> m_tmpSysKeycodeCallback;

    /// <summary>
    /// �w�q�@�ӨC�Ӱ�����U�۾֦����R�A thread_local �����ܼ� m_tlInstance�A���V HookManager ����C
    /// </summary>
    static thread_local HookManager* g_tlInstance;
};

