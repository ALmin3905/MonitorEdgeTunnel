#pragma once

#include <functional>
#include <string>
#include <string_view>
#include <source_location>

// Logger::Log 自動帶入 function name
#define LOG_WITH_CONTEXT(level, message) \
    Logger::Log(level, std::string("[") + std::source_location::current().function_name() + "] " + message);

/// <summary>
/// Logger (需注入log callback，否則只會輸出到console)
/// <para>注意!! 注入的log callback需要支援thread safe</para>
/// </summary>
class Logger
{
public:
    /// <summary>
    /// 定義的log level
    /// </summary>
    enum class LogLevel : int
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Fatal = 4
    };

    /// <summary>
    /// <param name="int">log level</param>
    /// <param name="const char*">log message</param>
    /// </summary>
    using LogCallback = std::function<void(int, const char*)>;

    static void SetLogCallback(LogCallback callback);

    static void Log(LogLevel level, std::string_view message);

    static void Log(int level, std::string_view message);

private:
    Logger() = default;

    ~Logger() = default;

    static Logger& GetInstance();

    void _SetLogCallback(LogCallback callback);

    void _Log(int level, std::string_view message);

    std::string LogLevelEnumToString(int level) const;

    LogCallback m_logCallback;
};

