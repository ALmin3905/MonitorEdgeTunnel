#include "pch.h"
#include "MonitorEdgeTunnelDll.h"
#include "MonitorEdgeTunnelManager.h"
#include <combaseapi.h>

extern "C"
{
    MONITOREDGETUNNELDLL_API bool __stdcall Start()
    {
        return MonitorEdgeTunnelManager::GetInstance().Start();
    }

    MONITOREDGETUNNELDLL_API bool __stdcall Stop()
    {
        return MonitorEdgeTunnelManager::GetInstance().Stop();
    }

    MONITOREDGETUNNELDLL_API bool __stdcall IsStart()
    {
        return MonitorEdgeTunnelManager::GetInstance().IsStart();
    }

    MONITOREDGETUNNELDLL_API void __stdcall SetKeycodeCallback(unsigned long keyCode, const KeycodeCallback callback)
    {
        MonitorEdgeTunnelManager::GetInstance().SetKeycodeCallback(keyCode, callback);
    }

    MONITOREDGETUNNELDLL_API void __stdcall GetMonitorInfoList(C_MonitorInfo** monitorInfoList, unsigned int* length)
    {
        // 初始化返回值
        *monitorInfoList = NULL;
        *length = 0;

        // 取得螢幕資訊
        const auto _monitorInfoList = MonitorEdgeTunnelManager::GetInstance().GetMonitorInfoList();
        if (_monitorInfoList.empty())
            return;

        // 配置記憶體
        size_t bufSize = sizeof(C_MonitorInfo) * _monitorInfoList.size();
        *monitorInfoList = (C_MonitorInfo*)::CoTaskMemAlloc(bufSize);
        if (*monitorInfoList)
            ::memset(*monitorInfoList, 0, bufSize);
        else
            return;

        // 賦值 (忽略警告)
        for (int i = 0; i < _monitorInfoList.size(); ++i)
        {
            (*monitorInfoList)[i].id = _monitorInfoList[i]->id;
            (*monitorInfoList)[i].top = _monitorInfoList[i]->top;
            (*monitorInfoList)[i].bottom = _monitorInfoList[i]->bottom;
            (*monitorInfoList)[i].left = _monitorInfoList[i]->left;
            (*monitorInfoList)[i].right = _monitorInfoList[i]->right;
            (*monitorInfoList)[i].scaling = _monitorInfoList[i]->scaling;
        }

        *length = _monitorInfoList.size();
    }

    MONITOREDGETUNNELDLL_API void __stdcall GetTunnelInfoList(C_TunnelInfo** tunnelInfoList, unsigned int* length)
    {
        // 初始化返回值
        *tunnelInfoList = NULL;
        *length = 0;

        // 取得tunnel資訊
        const auto _tunnelInfoList = MonitorEdgeTunnelManager::GetInstance().GetTunnelInfoList();
        if (_tunnelInfoList.empty())
            return;

        // 配置記憶體
        size_t bufSize = sizeof(C_TunnelInfo) * _tunnelInfoList.size();
        *tunnelInfoList = (C_TunnelInfo*)::CoTaskMemAlloc(bufSize);
        if (*tunnelInfoList)
            ::memset(*tunnelInfoList, 0, bufSize);
        else
            return;

        // 賦值 (忽略警告)
        for (int i = 0; i < _tunnelInfoList.size(); ++i)
        {
            (*tunnelInfoList)[i].id = _tunnelInfoList[i]->id;
            (*tunnelInfoList)[i].from = _tunnelInfoList[i]->from;
            (*tunnelInfoList)[i].to = _tunnelInfoList[i]->to;
            (*tunnelInfoList)[i].relativeID = _tunnelInfoList[i]->relativeID;
            (*tunnelInfoList)[i].displayID = _tunnelInfoList[i]->displayID;
            (*tunnelInfoList)[i].edgeType = static_cast<int>(_tunnelInfoList[i]->edgeType);
            (*tunnelInfoList)[i].rangeType = static_cast<int>(_tunnelInfoList[i]->rangeType);

        }

        *length = _tunnelInfoList.size();
    }

    MONITOREDGETUNNELDLL_API void __stdcall SetTunnelInfoList(C_TunnelInfo* tunnelInfoList, unsigned int length)
    {
        TunnelInfoList _tunnelInfoList;

        for (int i = 0; i < length; ++i)
        {
            auto _tunnelInfo = std::make_shared<TunnelInfo>();
            _tunnelInfo->id = tunnelInfoList[i].id;
            _tunnelInfo->from = tunnelInfoList[i].from;
            _tunnelInfo->to = tunnelInfoList[i].to;
            _tunnelInfo->relativeID = tunnelInfoList[i].relativeID;
            _tunnelInfo->displayID = tunnelInfoList[i].displayID;
            _tunnelInfo->edgeType = static_cast<EdgeType>(tunnelInfoList[i].edgeType);
            _tunnelInfo->rangeType = static_cast<RangeType>(tunnelInfoList[i].rangeType);

            _tunnelInfoList.push_back(_tunnelInfo);
        }

        MonitorEdgeTunnelManager::GetInstance().SetTunnelInfoList(_tunnelInfoList);
    }

    MONITOREDGETUNNELDLL_API bool __stdcall IsForceForbidEdge()
    {
        return MonitorEdgeTunnelManager::GetInstance().IsForceForbidEdge();
    }

    MONITOREDGETUNNELDLL_API void __stdcall SetForceForbidEdge(bool isForce)
    {
        MonitorEdgeTunnelManager::GetInstance().SetForceForbidEdge(isForce);
    }

    MONITOREDGETUNNELDLL_API void __stdcall SaveSetting()
    {
        MonitorEdgeTunnelManager::GetInstance().SaveSetting();
    }

    MONITOREDGETUNNELDLL_API void __stdcall LoadSetting()
    {
        MonitorEdgeTunnelManager::GetInstance().LoadSetting();
    }
}