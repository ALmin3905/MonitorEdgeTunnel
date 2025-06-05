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

        *length = static_cast<int>(_monitorInfoList.size());
    }

    MONITOREDGETUNNELDLL_API void __stdcall GetCurrentTunnelInfoList(C_TunnelInfo** tunnelInfoList, unsigned int* length)
    {
        // 初始化返回值
        *tunnelInfoList = nullptr;
        *length = 0;

        // 取得tunnel清單資訊
        TunnelInfoList _tunnelInfoList;
        {
            TunnelInfoListStruct _tunnelInfoListStruct;
            if (!MonitorEdgeTunnelManager::GetInstance().GetCurrentTunnelInfoListStruct(_tunnelInfoListStruct) || _tunnelInfoListStruct.tunnelInfoList.empty())
                return;

            _tunnelInfoList = std::move(_tunnelInfoListStruct.tunnelInfoList);
        }

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

        *length = static_cast<int>(_tunnelInfoList.size());
    }

    MONITOREDGETUNNELDLL_API bool __stdcall SetCurrentTunnelInfoList(C_TunnelInfo* tunnelInfoList, unsigned int length)
    {
        TunnelInfoList _tunnelInfoList;

        for (uint32_t i = 0; i < length; ++i)
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

        {
            TunnelInfoListStruct _tunnelInfoListStruct;
            MonitorEdgeTunnelManager::GetInstance().GetCurrentTunnelInfoListStruct(_tunnelInfoListStruct);

            _tunnelInfoListStruct.tunnelInfoList = std::move(_tunnelInfoList);

            if (!MonitorEdgeTunnelManager::GetInstance().SetCurrentTunnelInfoListStruct(_tunnelInfoListStruct))
                return false;
        }

        return true;
    }

    MONITOREDGETUNNELDLL_API bool __stdcall IsCurrentForceForbidEdge()
    {
        TunnelInfoListStruct _tunnelInfoListStruct;
        MonitorEdgeTunnelManager::GetInstance().GetCurrentTunnelInfoListStruct(_tunnelInfoListStruct);

        return _tunnelInfoListStruct.forceForbidEdge;
    }

    MONITOREDGETUNNELDLL_API void __stdcall SetCurrentForceForbidEdge(bool isForce)
    {
        TunnelInfoListStruct _tunnelInfoListStruct;
        MonitorEdgeTunnelManager::GetInstance().GetCurrentTunnelInfoListStruct(_tunnelInfoListStruct);

        _tunnelInfoListStruct.forceForbidEdge = isForce;

        MonitorEdgeTunnelManager::GetInstance().SetCurrentTunnelInfoListStruct(_tunnelInfoListStruct);
    }

    MONITOREDGETUNNELDLL_API void __stdcall SaveSetting()
    {
        MonitorEdgeTunnelManager::GetInstance().SaveSetting();
    }

    MONITOREDGETUNNELDLL_API bool __stdcall LoadSetting()
    {
        return MonitorEdgeTunnelManager::GetInstance().LoadSetting();
    }

    MONITOREDGETUNNELDLL_API int __stdcall GetErrorMsgCode()
    {
        return static_cast<int>(MonitorEdgeTunnelManager::GetInstance().GetErrorMsgCode());
    }
}
