#pragma once

#include "MonitorInfoManager.h"
#include "ThreadSafeObjectWrapper.h"

/// <summary>
/// �]�w�ɺ޲z
/// </summary>
class SettingManager
{
public:
    /// <summary>
    /// �q�D��T�M��Map (Load���J�BSave�x�s�F�����s������Ū�g)
    /// <para>���I�s get/get_read_only ���o������w�X�ݾ��A�b��w�X�ݾ��s�b���䤤�|�D�ʤWŪ�g��</para>
    /// </summary>
    ThreadSafeObjectWrapper<TunnelInfoListStructMap> TunnelInfoListStructMap;

    /// <summary>
    /// ���o���
    /// </summary>
    /// <returns>���</returns>
    static SettingManager& GetInstance();

    /// <summary>
    /// �x�s�ɮ�
    /// <para>�ФŦb�ϥ� TunnelInfoListStructMap �ɩI�s�A�|���ͦ���</para>
    /// </summary>
    void Save();

    /// <summary>
    /// ���J�ɮ�
    /// <para>�ФŦb�ϥ� TunnelInfoListStructMap �ɩI�s�A�|���ͦ���</para>
    /// </summary>
    void Load();

private:
    /// <summary>
    /// �غc�l
    /// </summary>
    SettingManager();

    /// <summary>
    /// �Ѻc�l
    /// </summary>
    ~SettingManager();
};

