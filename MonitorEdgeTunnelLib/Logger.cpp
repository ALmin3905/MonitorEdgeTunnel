#include "pch.h"
#include "Logger.h"
#include <iostream>

void Logger::SetLogCallback(LogCallback callback)
{
    GetInstance()._SetLogCallback(callback);
}

void Logger::Log(LogLevel level, std::string_view message)
{
    GetInstance()._Log(static_cast<int>(level), message);
}

void Logger::Log(int level, std::string_view message)
{
    GetInstance()._Log(level, message);
}

Logger& Logger::GetInstance()
{
    static Logger instance;
    return instance;
}

void Logger::_SetLogCallback(LogCallback callback)
{
    m_logCallback = callback;
}

void Logger::_Log(int level, std::string_view message)
{
    if (m_logCallback)
    {
        m_logCallback(level, message.data());
    }

    // Default logging behavior (e.g., output to console or file)
    std::cout << "Log [" << LogLevelEnumToString(level) << "]: " << message << std::endl;
}

std::string Logger::LogLevelEnumToString(int level) const
{
    switch (static_cast<LogLevel>(level))
    {
    case LogLevel::Debug:
        return "Debug";
    case LogLevel::Info:
        return "Info";
    case LogLevel::Warning:
        return "Warning";
    case LogLevel::Error:
        return "Error";
    case LogLevel::Fatal:
        return "Debug";
    default:
        return "Unknown";
    }
}
