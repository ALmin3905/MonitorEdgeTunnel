#pragma once

#include <string>
#include "MonitorInfoManager.h"

/// <summary>
/// �]�w�ɺ޲z
/// </summary>
class SettingManager
{
public:
    /// <summary>
    /// �q�D��T�M�� #(Load���J�BSave�x�s�F�����s������Ū�g)
    /// </summary>
    TunnelInfoList TunnelInfoList;

    /// <summary>
    /// �O�_�j��T��ù���t�q��(�Ȧ��q�D�W�h�~��q��) #(Load���J�BSave�x�s�F�����s������Ū�g)
    /// </summary>
    bool ForceForbidEdge;

    /// <summary>
    /// ���o���
    /// </summary>
    /// <returns>���</returns>
    static SettingManager& GetInstance();

    /// <summary>
    /// �x�s�ɮ�
    /// </summary>
    void Save();

    /// <summary>
    /// ���J�ɮ�
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

