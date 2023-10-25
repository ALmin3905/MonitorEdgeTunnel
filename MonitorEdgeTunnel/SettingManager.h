#pragma once

#include <string>
#include "MonitorInfoManager.h"

constexpr char DEFAULT_PATH[] = "setting.json";

class SettingManager
{
public:
    TunnelInfoList TunnelInfoList;

    bool ForceForbidEdge;

    static SettingManager& GetInstance();

    void Save(const std::string& path = DEFAULT_PATH);

    void Load(const std::string& path = DEFAULT_PATH);

private:
    SettingManager();

    ~SettingManager();
};

