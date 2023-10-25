#include "MouseEdgeManager.h"
#include <iostream>

/*static*/ MouseEdgeManager& MouseEdgeManager::GetInstance()
{
    static MouseEdgeManager instance;
    return instance;
}

MouseEdgeManager::MouseEdgeManager() : m_currMonitorInfo(nullptr), m_isInit(false)
{

}

MouseEdgeManager::~MouseEdgeManager()
{

}

void MouseEdgeManager::UpdateMonitorInfo(const MonitorInfoList monitorInfoList, bool isForceForbidEdge /*true*/)
{
    // 初始化
    m_monitorInfoList = monitorInfoList;
    m_currMonitorInfo = nullptr;
    m_isInit = false;

    // 紀錄tunnel
    RecordAllTunnelInfo();

    // 計算display from、to
    CalcDisplayFromTo();

    // 檢查tunnel是否有效
    CheckTunnelValid();

    if (isForceForbidEdge)
    {
        // 強制在所有螢幕邊界掛上禁止通行的tunnel，然後再重新計算、記錄一次tunnel
        ForceInsertForbidTunnelToAllEdge();
        RecordAllTunnelInfo();
        CalcDisplayFromTo();
    }

    // 計算TunnelInfo轉換的參數
    for (auto& tunnelInfo : m_tunnelInfoList)
        CalcTransportParam(tunnelInfo);

    // 取得當前滑鼠所在的MonitorInfo
    POINT pt;
    ::GetCursorPos(&pt);
    for (const auto& monitorInfo : m_monitorInfoList)
    {
        if (monitorInfo->left <= pt.x && pt.x <= monitorInfo->right && monitorInfo->top <= pt.y && pt.y <= monitorInfo->bottom)
        {
            m_currMonitorInfo = monitorInfo;
            break;
        }
    }

    // 檢查是否有找到MonitorInfo
    if (!m_currMonitorInfo)
        throw std::runtime_error("Failed to find MonitorInfo");

    m_isInit = true;
}

bool MouseEdgeManager::IsInit()
{
    return m_isInit;
}

bool MouseEdgeManager::EdgeTunnelTransport(POINT& pt)
{

#define ProcessTransport(_x, _y) \
    if (tunnelInfo->displayFrom <= _x && _x <= tunnelInfo->displayTo) \
    { \
        if (tunnelInfo->isPerpendicular) \
        { \
            _y = static_cast<int>(tunnelInfo->a * static_cast<double>(_x) + tunnelInfo->b); \
            _x = tunnelInfo->c; \
        } \
        else \
        { \
            _x = static_cast<int>(tunnelInfo->a * static_cast<double>(_x) + tunnelInfo->b); \
            _y = tunnelInfo->c; \
        } \
        if (!tunnelInfo->forbid) \
            m_currMonitorInfo = m_monitorInfoList[m_tunnelInfoList[tunnelInfo->relativeID]->displayID]; \
        return true; \
    } \

    // left
    if (pt.x < m_currMonitorInfo->left)
    {
        for (const auto& tunnelInfo : m_currMonitorInfo->leftTunnel)
        {
            ProcessTransport(pt.y, pt.x);
        }
    }

    // right
    if (pt.x > m_currMonitorInfo->right)
    {
        for (const auto& tunnelInfo : m_currMonitorInfo->rightTunnel)
        {
            ProcessTransport(pt.y, pt.x);
        }
    }

    // top
    if (pt.y < m_currMonitorInfo->top)
    {
        for (const auto& tunnelInfo : m_currMonitorInfo->topTunnel)
        {
            ProcessTransport(pt.x, pt.y);
        }
    }

    // bottom
    if (pt.y > m_currMonitorInfo->bottom)
    {
        for (const auto& tunnelInfo : m_currMonitorInfo->bottomTunnel)
        {
            ProcessTransport(pt.x, pt.y);
        }
    }

#undef ProcessTransport

    return false;
}

void MouseEdgeManager::ForceInsertForbidTunnelToAllEdge()
{
    // 安裝forbid tunnel到所有螢幕邊界
    int increaseID = static_cast<int>(m_tunnelInfoList.size());
    TunnelInfo tunnelInfo = { 0 };
    tunnelInfo.relativeID = -1;
    tunnelInfo.rangeType = RangeType::Full;
    for (const auto& monitorInfo : m_monitorInfoList)
    {
        // left
        tunnelInfo.id = increaseID++;
        monitorInfo->leftTunnel.push_back(std::make_shared<TunnelInfo>(tunnelInfo));

        // right
        tunnelInfo.id = increaseID++;
        monitorInfo->rightTunnel.push_back(std::make_shared<TunnelInfo>(tunnelInfo));

        // top
        tunnelInfo.id = increaseID++;
        monitorInfo->topTunnel.push_back(std::make_shared<TunnelInfo>(tunnelInfo));

        // bottom
        tunnelInfo.id = increaseID++;
        monitorInfo->bottomTunnel.push_back(std::make_shared<TunnelInfo>(tunnelInfo));
    }
}

void MouseEdgeManager::RecordAllTunnelInfo()
{
    // 初始化
    m_tunnelInfoList.clear();

    // 把螢幕上所有的tunnel info都放置到vector，可方便尋找對應的tunnel
    for (const auto& monitorInfo : m_monitorInfoList)
    {
        for (const auto& tunnelInfo : monitorInfo->leftTunnel)
        {
            tunnelInfo->displayID = monitorInfo->id;
            tunnelInfo->edgeType = EdgeType::Left;
            InsertTunnelInfo(tunnelInfo);
        }

        for (const auto& tunnelInfo : monitorInfo->rightTunnel)
        {
            tunnelInfo->displayID = monitorInfo->id;
            tunnelInfo->edgeType = EdgeType::Right;
            InsertTunnelInfo(tunnelInfo);
        }

        for (const auto& tunnelInfo : monitorInfo->topTunnel)
        {
            tunnelInfo->displayID = monitorInfo->id;
            tunnelInfo->edgeType = EdgeType::Top;
            InsertTunnelInfo(tunnelInfo);
        }

        for (const auto& tunnelInfo : monitorInfo->bottomTunnel)
        {
            tunnelInfo->displayID = monitorInfo->id;
            tunnelInfo->edgeType = EdgeType::Bottom;
            InsertTunnelInfo(tunnelInfo);
        }
    }
}

void MouseEdgeManager::InsertTunnelInfo(const std::shared_ptr<TunnelInfo>& tunnelInfo)
{
    if (tunnelInfo->id < 0)
        throw std::runtime_error("Tunnel ID less than 0");
    else if (tunnelInfo->id > 100)
        throw std::runtime_error("Tunnel ID more than 100");

    // 檢查長度是否足夠
    if (m_tunnelInfoList.size() <= tunnelInfo->id)
        m_tunnelInfoList.resize(static_cast<size_t>(tunnelInfo->id + 1), nullptr);

    // 檢查id是否已存在
    if (m_tunnelInfoList[tunnelInfo->id])
        throw std::runtime_error("Tunnel ID is repetitive");

    // 將info放至id對應的index
    m_tunnelInfoList[tunnelInfo->id] = tunnelInfo;
}

void MouseEdgeManager::CalcDisplayFromTo()
{
    for (auto& tunnelInfo : m_tunnelInfoList)
    {
        // 先依據EdgeType設定
        const auto& monitorInfo = m_monitorInfoList[tunnelInfo->displayID];
        switch (tunnelInfo->edgeType)
        {
        case EdgeType::Left:
        case EdgeType::Right:
            tunnelInfo->displayFrom = monitorInfo->top;
            tunnelInfo->displayTo = monitorInfo->bottom;
            break;
        case EdgeType::Top:
        case EdgeType::Bottom:
            tunnelInfo->displayFrom = monitorInfo->left;
            tunnelInfo->displayTo = monitorInfo->right;
            break;
        default:
            throw std::runtime_error("Failed to calc display from、to");
        }

        // 再依據RangeType修改
        switch (tunnelInfo->rangeType)
        {
        case RangeType::Full:
            // 什麼都不用做
            break;
        case RangeType::Relative:
            // offset
            tunnelInfo->displayFrom += tunnelInfo->from;
            tunnelInfo->displayTo -= tunnelInfo->to;
            break;
        case RangeType::Customize:
            // 自訂
            tunnelInfo->displayFrom += tunnelInfo->from;
            tunnelInfo->displayTo = tunnelInfo->displayFrom + tunnelInfo->to;
            break;
        default:
            throw std::runtime_error("Failed to calc display from、to");
        }
    }
}

void MouseEdgeManager::CheckTunnelValid()
{
    for (const auto& tunnelInfo : m_tunnelInfoList)
    {
        // 檢查tunnel是否有超出螢幕邊界
        {
            int from, to;

            const auto& monitorInfo = m_monitorInfoList[tunnelInfo->displayID];
            switch (tunnelInfo->edgeType)
            {
            case EdgeType::Left:
            case EdgeType::Right:
                from = monitorInfo->top;
                to = monitorInfo->bottom;
                break;
            case EdgeType::Top:
            case EdgeType::Bottom:
                from = monitorInfo->left;
                to = monitorInfo->right;
                break;
            default:
                throw std::runtime_error("Failed to check tunnel valid");
            }

            if (tunnelInfo->displayFrom < from || to < tunnelInfo->displayTo)
                throw std::runtime_error("tunnel range is invalid");
        }

        // 確認TunnelInfo都有對應的tunnel
        if (tunnelInfo->relativeID >= 0 && (tunnelInfo->relativeID == tunnelInfo->id || !m_tunnelInfoList[tunnelInfo->relativeID]))
            throw std::runtime_error("Tunnel info bind Invalid relative ID");
    }
}

void MouseEdgeManager::CalcTransportParam(std::shared_ptr<TunnelInfo>& tunnelInfo)
{
    // 轉換線性方程式(y = ax + b)，可先計算a、b為多少
    // 至於c則是對應tunnel的邊界值
    // 轉換滑鼠座標會受到windows縮放比例影響，所以要先行一步處理，避免錯置滑鼠位置

    if (tunnelInfo->relativeID >= 0)
    {
        auto& relativeTunnelInfo = m_tunnelInfoList[tunnelInfo->relativeID];
        const auto& relativeMonitorInfo = m_monitorInfoList[relativeTunnelInfo->displayID];
        double f = tunnelInfo->displayFrom;
        double t = tunnelInfo->displayTo;
        double rf = relativeTunnelInfo->displayFrom;
        double rt = relativeTunnelInfo->displayTo;
        double rs = relativeMonitorInfo->scaling;

        double tmp1 = (rt - rf) / (t - f) / rs;
        double tmp2 = rf / rs;

        // a、b
        tunnelInfo->a = tmp1;
        tunnelInfo->b = tmp2 - tmp1 * f;

        // c
        switch (relativeTunnelInfo->edgeType)
        {
        case EdgeType::Left:
            tunnelInfo->c = relativeMonitorInfo->left;
            break;
        case EdgeType::Right:
            tunnelInfo->c = relativeMonitorInfo->right;
            break;
        case EdgeType::Top:
            tunnelInfo->c = relativeMonitorInfo->top;
            break;
        case EdgeType::Bottom:
            tunnelInfo->c = relativeMonitorInfo->bottom;
            break;
        default:
            throw std::runtime_error("EdgeType Error : out of range");
        }
        tunnelInfo->c = static_cast<int>(static_cast<double>(tunnelInfo->c) / rs);

        // 是否禁止通行
        tunnelInfo->forbid = false;

        // 是否垂直
        switch (tunnelInfo->edgeType)
        {
        case EdgeType::Left:
        case EdgeType::Right:
            tunnelInfo->isPerpendicular = true;
            break;
        case EdgeType::Top:
        case EdgeType::Bottom:
            tunnelInfo->isPerpendicular = false;
            break;
        default:
            throw std::runtime_error("EdgeType Error : out of range");
        }

        switch (relativeTunnelInfo->edgeType)
        {
        case EdgeType::Left:
        case EdgeType::Right:
            tunnelInfo->isPerpendicular ^= true;
            break;
        case EdgeType::Top:
        case EdgeType::Bottom:
            tunnelInfo->isPerpendicular ^= false;
            break;
        default:
            throw std::runtime_error("EdgeType Error : out of range");
        }
    }
    else
    {
        const auto& monitorInfo = m_monitorInfoList[tunnelInfo->displayID];
        double s = monitorInfo->scaling;

        // a、b
        tunnelInfo->a = 1 / s;
        tunnelInfo->b = 0;

        // c
        switch (tunnelInfo->edgeType)
        {
        case EdgeType::Left:
            tunnelInfo->c = monitorInfo->left;
            break;
        case EdgeType::Right:
            tunnelInfo->c = monitorInfo->right;
            break;
        case EdgeType::Top:
            tunnelInfo->c = monitorInfo->top;
            break;
        case EdgeType::Bottom:
            tunnelInfo->c = monitorInfo->bottom;
            break;
        default:
            throw std::runtime_error("EdgeType Error : out of range");
        }
        tunnelInfo->c = static_cast<int>(static_cast<double>(tunnelInfo->c) / s);

        // 是否禁止通行 (relativeID = -1 就是禁止通行)
        tunnelInfo->forbid = true;

        // 是否垂直 (禁止通行就不會是垂直)
        tunnelInfo->isPerpendicular = false;
    }
}
