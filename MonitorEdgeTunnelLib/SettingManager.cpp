#include "pch.h"
#include "SettingManager.h"
#include <Windows.h>
#include <pathcch.h>
#include <fstream>
#include <tchar.h>
#include "json.hpp"
using json = nlohmann::json;

NLOHMANN_JSON_SERIALIZE_ENUM(EdgeType, {
    {EdgeType::Left, "Left"},
    {EdgeType::Right, "Right"},
    {EdgeType::Top, "Top"},
    {EdgeType::Bottom, "Bottom"},
})

NLOHMANN_JSON_SERIALIZE_ENUM(RangeType, {
    {RangeType::Full, "Full"},
    {RangeType::Relative, "Relative"},
    {RangeType::Customize, "Customize"},
})

namespace
{
    constexpr TCHAR SETTING_FILE_NAME[] = _T("\\setting.json");

    constexpr char TUNNELINFOLIST_KEY[] = "TunnelInfoList";
    constexpr char TUNNELINFO_ID_KEY[] = "id";
    constexpr char TUNNELINFO_DISPLAYID_KEY[] = "displayID";
    constexpr char TUNNELINFO_RELATIVEID_KEY[] = "relativeID";
    constexpr char TUNNELINFO_EDGETYPE_KEY[] = "edgeType";
    constexpr char TUNNELINFO_RANGETYPE_KEY[] = "rangeType";
    constexpr char TUNNELINFO_FROM_KEY[] = "from";
    constexpr char TUNNELINFO_TO_KEY[] = "to";
    constexpr char FORCEFORBIDEDGE_KEY[] = "forceForbidEdge";

    TCHAR ExeFilePath[MAX_PATH];
}

/*static*/ SettingManager& SettingManager::GetInstance()
{
    static SettingManager instance;
    return instance;
}

SettingManager::SettingManager() : ForceForbidEdge(false)
{
    // 取得執行檔位置
    ::ZeroMemory(ExeFilePath, sizeof(ExeFilePath));
    ::GetModuleFileName(NULL, ExeFilePath, MAX_PATH);
    ::PathCchRemoveFileSpec(ExeFilePath, MAX_PATH);

    // 加上設定檔名稱
    ::PathCchAppend(ExeFilePath, MAX_PATH, SETTING_FILE_NAME);
}

SettingManager::~SettingManager()
{

}

void SettingManager::Save()
{
    // 建立json
    json data;

    // 建立TunnelInfoList
    data[TUNNELINFOLIST_KEY] = json::array();
    for (const auto& tunnelInfo : TunnelInfoList)
    {
        json jTunnelInfo;
        jTunnelInfo[TUNNELINFO_ID_KEY] = tunnelInfo->id;
        jTunnelInfo[TUNNELINFO_DISPLAYID_KEY] = tunnelInfo->displayID;
        jTunnelInfo[TUNNELINFO_RELATIVEID_KEY] = tunnelInfo->relativeID;
        jTunnelInfo[TUNNELINFO_EDGETYPE_KEY] = tunnelInfo->edgeType;
        jTunnelInfo[TUNNELINFO_RANGETYPE_KEY] = tunnelInfo->rangeType;
        jTunnelInfo[TUNNELINFO_FROM_KEY] = tunnelInfo->from;
        jTunnelInfo[TUNNELINFO_TO_KEY] = tunnelInfo->to;

        data[TUNNELINFOLIST_KEY].push_back(std::move(jTunnelInfo));
    }

    // 建立 ForceForbidEdge
    data[FORCEFORBIDEDGE_KEY] = ForceForbidEdge;

    // 寫入檔案
    std::ofstream f(ExeFilePath);
    f << data.dump(4);
}

void SettingManager::Load()
{
    // 清空資料
    TunnelInfoList.clear();
    ForceForbidEdge = false;

    // 讀取json
    std::ifstream f(ExeFilePath);
    json data = json::parse(f);

    // 取得TunnelInfo
    for (const json& jTunnelInfo : data[TUNNELINFOLIST_KEY])
    {
        // json轉換成TunnelInfo
        auto tunnelInfo = std::make_shared<TunnelInfo>();
        tunnelInfo->id = jTunnelInfo[TUNNELINFO_ID_KEY].template get<int>();
        tunnelInfo->displayID = jTunnelInfo[TUNNELINFO_DISPLAYID_KEY].template get<int>();
        tunnelInfo->relativeID = jTunnelInfo[TUNNELINFO_RELATIVEID_KEY].template get<int>();
        tunnelInfo->edgeType = jTunnelInfo[TUNNELINFO_EDGETYPE_KEY].template get<EdgeType>();
        tunnelInfo->rangeType = jTunnelInfo[TUNNELINFO_RANGETYPE_KEY].template get<RangeType>();
        tunnelInfo->from = jTunnelInfo[TUNNELINFO_FROM_KEY].template get<int>();
        tunnelInfo->to = jTunnelInfo[TUNNELINFO_TO_KEY].template get<int>();

        TunnelInfoList.push_back(std::move(tunnelInfo));
    }

    // 取得 ForceForbidEdge
    ForceForbidEdge = data[FORCEFORBIDEDGE_KEY].template get<bool>();
}