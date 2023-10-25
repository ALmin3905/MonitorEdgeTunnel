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
    // ��l��
    m_monitorInfoList = monitorInfoList;
    m_currMonitorInfo = nullptr;
    m_isInit = false;

    // ����tunnel
    RecordAllTunnelInfo();

    // �p��display from�Bto
    CalcDisplayFromTo();

    // �ˬdtunnel�O�_����
    CheckTunnelValid();

    if (isForceForbidEdge)
    {
        // �j��b�Ҧ��ù���ɱ��W�T��q�檺tunnel�A�M��A���s�p��B�O���@��tunnel
        ForceInsertForbidTunnelToAllEdge();
        RecordAllTunnelInfo();
        CalcDisplayFromTo();
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
    // �w��forbid tunnel��Ҧ��ù����
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
    // ��l��
    m_tunnelInfoList.clear();

    // ��ù��W�Ҧ���tunnel info����m��vector�A�i��K�M�������tunnel
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

    // �ˬd���׬O�_����
    if (m_tunnelInfoList.size() <= tunnelInfo->id)
        m_tunnelInfoList.resize(static_cast<size_t>(tunnelInfo->id + 1), nullptr);

    // �ˬdid�O�_�w�s�b
    if (m_tunnelInfoList[tunnelInfo->id])
        throw std::runtime_error("Tunnel ID is repetitive");

    // �Ninfo���id������index
    m_tunnelInfoList[tunnelInfo->id] = tunnelInfo;
}

void MouseEdgeManager::CalcDisplayFromTo()
{
    for (auto& tunnelInfo : m_tunnelInfoList)
    {
        // ���̾�EdgeType�]�w
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
            throw std::runtime_error("Failed to calc display from�Bto");
        }

        // �A�̾�RangeType�ק�
        switch (tunnelInfo->rangeType)
        {
        case RangeType::Full:
            // ���򳣤��ΰ�
            break;
        case RangeType::Relative:
            // offset
            tunnelInfo->displayFrom += tunnelInfo->from;
            tunnelInfo->displayTo -= tunnelInfo->to;
            break;
        case RangeType::Customize:
            // �ۭq
            tunnelInfo->displayFrom += tunnelInfo->from;
            tunnelInfo->displayTo = tunnelInfo->displayFrom + tunnelInfo->to;
            break;
        default:
            throw std::runtime_error("Failed to calc display from�Bto");
        }
    }
}

void MouseEdgeManager::CheckTunnelValid()
{
    for (const auto& tunnelInfo : m_tunnelInfoList)
    {
        // �ˬdtunnel�O�_���W�X�ù����
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
        const auto& relativeMonitorInfo = m_monitorInfoList[relativeTunnelInfo->displayID];
        double f = tunnelInfo->displayFrom;
        double t = tunnelInfo->displayTo;
        double rf = relativeTunnelInfo->displayFrom;
        double rt = relativeTunnelInfo->displayTo;
        double rs = relativeMonitorInfo->scaling;

        double tmp1 = (rt - rf) / (t - f) / rs;
        double tmp2 = rf / rs;

        // a�Bb
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

        // �O�_�T��q��
        tunnelInfo->forbid = false;

        // �O�_����
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

        // a�Bb
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

        // �O�_�T��q�� (relativeID = -1 �N�O�T��q��)
        tunnelInfo->forbid = true;

        // �O�_���� (�T��q��N���|�O����)
        tunnelInfo->isPerpendicular = false;
    }
}
