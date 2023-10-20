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

void MouseEdgeManager::UpdateMonitorInfo(const std::vector<std::shared_ptr<MonitorInfo>>& monitorInfoList, bool isForceForbidEdge /*true*/)
{
    // ��l��
    m_monitorInfoList = monitorInfoList;
    m_currMonitorInfo = nullptr;
    m_isInit = false;

    // ����tunnel
    RecordAllTunnelInfo();

    // �ˬdtunnel�O�_����
    CheckTunnelValid();

    if (isForceForbidEdge)
    {
        // �j��b�Ҧ��ù���ɱ��W�T��q�檺tunnel�A�M��A���s�O���@��tunnel
        ForceInsertForbidTunnelToAllEdge();
        RecordAllTunnelInfo();
    }

    // �p��TunnelInfo�ഫ���Ѽ�
    for (auto& tunnelInfo : m_tunnelInfoList)
        CalcTransportParam(tunnelInfo);

    // ���o��e�ƹ��Ҧb��MonitorInfo
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

    // �ˬd�O�_�����MonitorInfo
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
    if (tunnelInfo->from <= _x && _x <= tunnelInfo->to) \
    { \
        _x = static_cast<int>(tunnelInfo->a * static_cast<double>(_x) + tunnelInfo->b); \
        _y = tunnelInfo->c; \
        if (!tunnelInfo->forbid) \
            m_currMonitorInfo = m_monitorInfoList[m_tunnelInfoList[tunnelInfo->relativeID]->deviceID]; \
        return true; \
    } \

    // left
    if (pt.x < m_currMonitorInfo->left)
    {
        for (const auto& tunnelInfo : m_currMonitorInfo->leftTunnel)
        {
            ProcessTransport(pt.y, pt.x)
        }
    }

    // right
    if (pt.x > m_currMonitorInfo->right)
    {
        for (const auto& tunnelInfo : m_currMonitorInfo->rightTunnel)
        {
            ProcessTransport(pt.y, pt.x)
        }
    }

    // top
    if (pt.y < m_currMonitorInfo->top)
    {
        for (const auto& tunnelInfo : m_currMonitorInfo->topTunnel)
        {
            ProcessTransport(pt.x, pt.y)
        }
    }

    // bottom
    if (pt.y > m_currMonitorInfo->bottom)
    {
        for (const auto& tunnelInfo : m_currMonitorInfo->bottomTunnel)
        {
            ProcessTransport(pt.x, pt.y)
        }
    }

#undef ProcessTransport

    return false;
}

void MouseEdgeManager::ForceInsertForbidTunnelToAllEdge()
{
    // �w��forbid tunnel��Ҧ��ù����
    int increaseID = static_cast<int>(m_tunnelInfoList.size());
    TunnelInfo tunnelInfo = { 0 };
    tunnelInfo.relativeID = -1;
    for (const auto& monitorInfo : m_monitorInfoList)
    {
        // left
        tunnelInfo.id = increaseID++;
        tunnelInfo.from = monitorInfo->top;
        tunnelInfo.to = monitorInfo->bottom;
        monitorInfo->leftTunnel.emplace_back(std::make_shared<TunnelInfo>(tunnelInfo));

        // right
        tunnelInfo.id = increaseID++;
        monitorInfo->rightTunnel.emplace_back(std::make_shared<TunnelInfo>(tunnelInfo));

        // top
        tunnelInfo.id = increaseID++;
        tunnelInfo.from = monitorInfo->left;
        tunnelInfo.to = monitorInfo->right;
        monitorInfo->topTunnel.emplace_back(std::make_shared<TunnelInfo>(tunnelInfo));

        // bottom
        tunnelInfo.id = increaseID++;
        monitorInfo->bottomTunnel.emplace_back(std::make_shared<TunnelInfo>(tunnelInfo));
    }
}

void MouseEdgeManager::RecordAllTunnelInfo()
{
    // ��l��
    m_tunnelInfoList.clear();

    // ��ù��W�Ҧ���tunnel info����m��vector�A�i��K�M�������tunnel
    for (const auto& monitorInfo : m_monitorInfoList)
    {
        for (const auto& tunnelInfo : monitorInfo->leftTunnel)
        {
            tunnelInfo->type = EdgeType::Left;
            tunnelInfo->deviceID = monitorInfo->id;
            InsertTunnelInfo(tunnelInfo);
        }

        for (const auto& tunnelInfo : monitorInfo->rightTunnel)
        {
            tunnelInfo->type = EdgeType::Right;
            tunnelInfo->deviceID = monitorInfo->id;
            InsertTunnelInfo(tunnelInfo);
        }

        for (const auto& tunnelInfo : monitorInfo->topTunnel)
        {
            tunnelInfo->type = EdgeType::Top;
            tunnelInfo->deviceID = monitorInfo->id;
            InsertTunnelInfo(tunnelInfo);
        }

        for (const auto& tunnelInfo : monitorInfo->bottomTunnel)
        {
            tunnelInfo->type = EdgeType::Bottom;
            tunnelInfo->deviceID = monitorInfo->id;
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

    // �ˬd���׬O�_����
    if (m_tunnelInfoList.size() <= tunnelInfo->id)
        m_tunnelInfoList.resize(tunnelInfo->id + 1, nullptr);

    // �ˬdid�O�_�w�s�b
    if (m_tunnelInfoList[tunnelInfo->id])
        throw std::runtime_error("Tunnel ID is repetitive");

    // �Ninfo���id������index
    m_tunnelInfoList[tunnelInfo->id] = tunnelInfo;
}

void MouseEdgeManager::CheckTunnelValid()
{
    for (const auto& tunnelInfo : m_tunnelInfoList)
    {
        // �ˬdtunnel�O�_���W�X�ù����
        {
            int from, to;

            const auto& monitorInfo = m_monitorInfoList[tunnelInfo->deviceID];
            switch (tunnelInfo->type)
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

            if (tunnelInfo->from < from || to < tunnelInfo->to)
                throw std::runtime_error("tunnel range is invalid");
        }

        // �T�{TunnelInfo����������tunnel
        if (tunnelInfo->relativeID >= 0 && (tunnelInfo->relativeID == tunnelInfo->id || !m_tunnelInfoList[tunnelInfo->relativeID]))
            throw std::runtime_error("Tunnel info bind Invalid relative ID");
    }
}

void MouseEdgeManager::CalcTransportParam(std::shared_ptr<TunnelInfo>& tunnelInfo)
{
    // �ഫ�u�ʤ�{��(y = ax + b)�A�i���p��a�Bb���h��
    // �ܩ�c�h�O����tunnel����ɭ�
    // �ഫ�ƹ��y�з|����windows�Y���Ҽv�T�A�ҥH�n����@�B�B�z�A�קK���m�ƹ���m

    if (tunnelInfo->relativeID >= 0)
    {
        auto& relativeTunnelInfo = m_tunnelInfoList[tunnelInfo->relativeID];
        const auto& relativeMonitorInfo = m_monitorInfoList[relativeTunnelInfo->deviceID];
        double f = tunnelInfo->from;
        double t = tunnelInfo->to;
        double rf = relativeTunnelInfo->from;
        double rt = relativeTunnelInfo->to;
        double rs = relativeMonitorInfo->scaling;

        double a = (rt - rf) / (t - f) / rs;
        double b = rf / rs;

        tunnelInfo->a = a;
        tunnelInfo->b = b - a * f;
        tunnelInfo->forbid = false;

        switch (relativeTunnelInfo->type)
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
    }
    else
    {
        const auto& monitorInfo = m_monitorInfoList[tunnelInfo->deviceID];
        double s = monitorInfo->scaling;

        tunnelInfo->a = 1 / s;
        tunnelInfo->b = 0;
        tunnelInfo->forbid = true;

        switch (tunnelInfo->type)
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
    }
}
