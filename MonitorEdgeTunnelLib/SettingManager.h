#pragma once

#include <string>
#include <unordered_map>
#include "MonitorInfoManager.h"

/// <summary>
/// �w�q�q�D�M��Map�AKey���ù���T�M��base64�s�X�A�Шϥ�"MonitorInfoManager::GetMonitorInfoListBase64"���o
/// </summary>
typedef std::unordered_map<std::string, TunnelInfoList> TunnelInfoListMap;

/// <summary>
/// �]�w�ɺ޲z
/// </summary>
class SettingManager
{
public:
    /// <summary>
    /// �q�D��T�M��Map #(Load���J�BSave�x�s�F�����s������Ū�g)
    /// </summary>
    TunnelInfoListMap TunnelInfoListMap;

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

