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
    // ���o�����ɦ�m
    ::ZeroMemory(ExeFilePath, sizeof(ExeFilePath));
    ::GetModuleFileName(NULL, ExeFilePath, MAX_PATH);
    ::PathCchRemoveFileSpec(ExeFilePath, MAX_PATH);

    // �[�W�]�w�ɦW��
    ::PathCchAppend(ExeFilePath, MAX_PATH, SETTING_FILE_NAME);
}

SettingManager::~SettingManager()
{

}

void SettingManager::Save()
{
    // �إ�json
    json data;

    // �إ�TunnelInfoList
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

    // �إ� ForceForbidEdge
    data[FORCEFORBIDEDGE_KEY] = ForceForbidEdge;

    // �g�J�ɮ�
    std::ofstream f(ExeFilePath);
    f << data.dump(4);
}

void SettingManager::Load()
{
    // �M�Ÿ��
    TunnelInfoList.clear();
    ForceForbidEdge = false;

    // Ū��json
    std::ifstream f(ExeFilePath);
    json data = json::parse(f);

    // ���oTunnelInfo
    for (const json& jTunnelInfo : data[TUNNELINFOLIST_KEY])
    {
        // json�ഫ��TunnelInfo
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

    // ���o ForceForbidEdge
    ForceForbidEdge = data[FORCEFORBIDEDGE_KEY].template get<bool>();
}