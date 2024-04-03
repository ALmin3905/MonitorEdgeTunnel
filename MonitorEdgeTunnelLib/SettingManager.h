#pragma once

#include "MonitorInfoManager.h"

/// <summary>
/// �]�w�ɺ޲z�C
/// �����O�õL������w���A���Ū�g�B�s�ɡBŪ�ɵ��Цۦ�x���C
/// </summary>
class SettingManager
{
public:
    /// <summary>
    /// �q�D��T�M��Map #(Load���J�BSave�x�s�F�����s������Ū�g)
    /// </summary>
    TunnelInfoListStructMap TunnelInfoListStructMap;

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

