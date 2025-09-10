#include "pch.h"
#include "SettingManager.h"
#include <Windows.h>
#include <pathcch.h>
#include <fstream>
#include <tchar.h>
#include "Logger.h"
#include "Utility.h"
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
                DWORD sz = ::GetModuleFileNameW(NULL, SettingFilePath, MAX_PATH);
                if (sz == 0 || sz >= MAX_PATH)
                {
                    // 取得失敗
                    LOG_WITH_CONTEXT(Logger::LogLevel::Error, "GetModuleFileName failed, size: " + std::to_string(sz) + ", ErrorCode: " + std::to_string(GetLastError()));
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
                    LOG_WITH_CONTEXT(Logger::LogLevel::Error, "PathCchRemoveFileSpec failed, path: " + Utility::wchar_to_utf8(SettingFilePath));
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
                    LOG_WITH_CONTEXT(Logger::LogLevel::Error, "PathCchAppend failed, HRESULT: " + std::to_string(res));
                    ::ZeroMemory(SettingFilePath, sizeof(SettingFilePath));
                    return;
                }
            }
        });

        return SettingFilePath;
    }
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
            jTunnelInfo[TUNNELINFO_ID_KEY] = tunnelInfo.id;
            jTunnelInfo[TUNNELINFO_DISPLAYID_KEY] = tunnelInfo.displayID;
            jTunnelInfo[TUNNELINFO_RELATIVEID_KEY] = tunnelInfo.relativeID;
            jTunnelInfo[TUNNELINFO_EDGETYPE_KEY] = tunnelInfo.edgeType;
            jTunnelInfo[TUNNELINFO_RANGETYPE_KEY] = tunnelInfo.rangeType;
            jTunnelInfo[TUNNELINFO_FROM_KEY] = tunnelInfo.from;
            jTunnelInfo[TUNNELINFO_TO_KEY] = tunnelInfo.to;

            data[tunnelInfoListStructPair.first][TUNNELINFOLIST_KEY].push_back(std::move(jTunnelInfo));
        }

        // forceForbidEdge
        data[tunnelInfoListStructPair.first][FORCEFORBIDEDGE_KEY] = tunnelInfoListStructPair.second.forceForbidEdge;
    }

    // 開檔案
    std::ofstream f;
    {
        std::filesystem::path fsp(GetSettingFilePath());
        f.open(fsp);
        if (!f.is_open())
        {
            // 無法開啟檔案
            throw std::runtime_error("Failed to open setting file: " + fsp.string());
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
        f.open(fsp);
        if (!f.is_open())
        {
            // 無法開啟檔案
            throw std::runtime_error("Failed to open setting file: " + fsp.string());
        }
    }

    // 檢查是否為空檔案
    if (f.peek() == std::ifstream::traits_type::eof())
        return;

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
                TunnelInfo tunnelInfo{};
                tunnelInfo.id = jTunnelInfo.value()[TUNNELINFO_ID_KEY].template get<int>();
                tunnelInfo.displayID = jTunnelInfo.value()[TUNNELINFO_DISPLAYID_KEY].template get<int>();
                tunnelInfo.relativeID = jTunnelInfo.value()[TUNNELINFO_RELATIVEID_KEY].template get<int>();
                tunnelInfo.edgeType = jTunnelInfo.value()[TUNNELINFO_EDGETYPE_KEY].template get<EdgeType>();
                tunnelInfo.rangeType = jTunnelInfo.value()[TUNNELINFO_RANGETYPE_KEY].template get<RangeType>();
                tunnelInfo.from = jTunnelInfo.value()[TUNNELINFO_FROM_KEY].template get<int>();
                tunnelInfo.to = jTunnelInfo.value()[TUNNELINFO_TO_KEY].template get<int>();

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
