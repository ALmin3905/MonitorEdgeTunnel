#include "pch.h"
#include "SettingManager.h"
#include <Windows.h>
#include <pathcch.h>
#include <fstream>
#include <tchar.h>
#include "json.hpp"
using json = nlohmann::json;

/// <summary>
/// 注意!! 使用的json庫須使用utf-8編碼，因此使用時需要注意子串的編碼格式，特別是超出ASCII以外的字串
/// 所以盡量不要使用中文字串，否則可能會導致json解析錯誤
/// </summary>

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
    constexpr auto SETTING_FILE_NAME = L"\\setting.json";

    constexpr auto TUNNELINFOLIST_KEY = "TunnelInfoList";
    constexpr auto TUNNELINFO_ID_KEY = "id";
    constexpr auto TUNNELINFO_DISPLAYID_KEY = "displayID";
    constexpr auto TUNNELINFO_RELATIVEID_KEY = "relativeID";
    constexpr auto TUNNELINFO_EDGETYPE_KEY = "edgeType";
    constexpr auto TUNNELINFO_RANGETYPE_KEY = "rangeType";
    constexpr auto TUNNELINFO_FROM_KEY = "from";
    constexpr auto TUNNELINFO_TO_KEY = "to";
    constexpr auto FORCEFORBIDEDGE_KEY = "forceForbidEdge";

    /// <summary>
    /// 取得設定檔的完整路徑
    /// </summary>
    const wchar_t* GetSettingFilePath()
    {
        static wchar_t SettingFilePath[MAX_PATH]{};
        static std::once_flag flag;

        std::call_once(flag, []() {
            ::ZeroMemory(SettingFilePath, sizeof(SettingFilePath));

            // 取得執行檔位置
            {
                DWORD sz = ::GetModuleFileName(NULL, SettingFilePath, MAX_PATH);
                if (sz == 0 || sz >= MAX_PATH)
                {
                    // 取得失敗
                    ::ZeroMemory(SettingFilePath, sizeof(SettingFilePath));
                    return;
                }
            }

            // 移除檔案名稱，保留路徑
            {
                HRESULT res = ::PathCchRemoveFileSpec(SettingFilePath, MAX_PATH);
                if (res != S_OK)
                {
                    // 移除檔案路徑失敗
                    ::ZeroMemory(SettingFilePath, sizeof(SettingFilePath));
                    return;
                }
            }

            // 加上設定檔名稱
            {
                HRESULT res = ::PathCchAppend(SettingFilePath, MAX_PATH, SETTING_FILE_NAME);
                if (res != S_OK)
                {
                    // 加上設定檔名稱失敗
                    ::ZeroMemory(SettingFilePath, sizeof(SettingFilePath));
                    return;
                }
            }
        });

        return SettingFilePath;
    }
}

SettingManager& SettingManager::GetInstance()
{
    static SettingManager instance;
    return instance;
}

SettingManager::SettingManager()
{

}

SettingManager::~SettingManager()
{

}

void SettingManager::Save()
{
    auto tunnelInfoListStructMap = TunnelInfoListStructMap.get_readonly();

    // 建立json
    json data = json::object();

    for (const auto& tunnelInfoListStructPair : *tunnelInfoListStructMap)
    {
        // 將base64編碼作為key
        data[tunnelInfoListStructPair.first] = json::object();

        // tunnelInfoList
        data[tunnelInfoListStructPair.first][TUNNELINFOLIST_KEY] = json::array();
        for (const auto& tunnelInfo : tunnelInfoListStructPair.second.tunnelInfoList)
        {
            json jTunnelInfo;
            jTunnelInfo[TUNNELINFO_ID_KEY] = tunnelInfo->id;
            jTunnelInfo[TUNNELINFO_DISPLAYID_KEY] = tunnelInfo->displayID;
            jTunnelInfo[TUNNELINFO_RELATIVEID_KEY] = tunnelInfo->relativeID;
            jTunnelInfo[TUNNELINFO_EDGETYPE_KEY] = tunnelInfo->edgeType;
            jTunnelInfo[TUNNELINFO_RANGETYPE_KEY] = tunnelInfo->rangeType;
            jTunnelInfo[TUNNELINFO_FROM_KEY] = tunnelInfo->from;
            jTunnelInfo[TUNNELINFO_TO_KEY] = tunnelInfo->to;

            data[tunnelInfoListStructPair.first][TUNNELINFOLIST_KEY].push_back(std::move(jTunnelInfo));
        }

        // forceForbidEdge
        data[tunnelInfoListStructPair.first][FORCEFORBIDEDGE_KEY] = tunnelInfoListStructPair.second.forceForbidEdge;
    }

    // 開檔案
    std::ofstream f;
    {
        std::filesystem::path fsp(GetSettingFilePath());
        f.open(fsp.c_str());
        if (!f.is_open())
        {
            // 無法開啟檔案
            return;
        }
    }

    // 寫入檔案
    f << data.dump(4);
}

void SettingManager::Load()
{
    auto tunnelInfoListStructMap = TunnelInfoListStructMap.get();

    // 清空資料
    tunnelInfoListStructMap->clear();

    // 開檔案
    std::ifstream f;
    {
        std::filesystem::path fsp(GetSettingFilePath());
        f.open(fsp.c_str());
        if (!f.is_open())
        {
            // 無法開啟檔案
            return;
        }
    }

    // parse json
    json data = json::parse(f);

    // 取得TunnelInfoListStruct
    for (const auto& jTunnelInfoListStruct : data.items())
    {
        TunnelInfoListStruct tunnelInfoListStruct;

        // TunnelInfoList
        {
            TunnelInfoList tunnelInfoList;
            for (const auto& jTunnelInfo : jTunnelInfoListStruct.value()[TUNNELINFOLIST_KEY].items())
            {
                // json轉換成TunnelInfo
                auto tunnelInfo = std::make_shared<TunnelInfo>();
                tunnelInfo->id = jTunnelInfo.value()[TUNNELINFO_ID_KEY].template get<int>();
                tunnelInfo->displayID = jTunnelInfo.value()[TUNNELINFO_DISPLAYID_KEY].template get<int>();
                tunnelInfo->relativeID = jTunnelInfo.value()[TUNNELINFO_RELATIVEID_KEY].template get<int>();
                tunnelInfo->edgeType = jTunnelInfo.value()[TUNNELINFO_EDGETYPE_KEY].template get<EdgeType>();
                tunnelInfo->rangeType = jTunnelInfo.value()[TUNNELINFO_RANGETYPE_KEY].template get<RangeType>();
                tunnelInfo->from = jTunnelInfo.value()[TUNNELINFO_FROM_KEY].template get<int>();
                tunnelInfo->to = jTunnelInfo.value()[TUNNELINFO_TO_KEY].template get<int>();

                tunnelInfoList.push_back(std::move(tunnelInfo));
            }

            tunnelInfoListStruct.tunnelInfoList = std::move(tunnelInfoList);
        }

        // forceForbidEdge
        tunnelInfoListStruct.forceForbidEdge = jTunnelInfoListStruct.value()[FORCEFORBIDEDGE_KEY].template get<bool>();

        // 放入map
        (*tunnelInfoListStructMap)[jTunnelInfoListStruct.key()] = std::move(tunnelInfoListStruct);
    }
}