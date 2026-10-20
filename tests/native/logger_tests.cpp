#include "LoggerCapture.h"
#include "TestSupport.h"
#include <atomic>
#include <vector>

int main()
{
    return RunTests({
        { "callback receives bounded message", [] {
            LogCapture capture;
            std::string received;
            int receivedLevel = -1;
            Logger::SetLogCallback([&](int level, const char* text) {
                receivedLevel = level;
                received = text ? text : "<null>";
            });
            const std::string backing = "messageTAIL";
            Logger::Log(Logger::LogLevel::Warning, std::string_view(backing).substr(0, 7));
            Check(received == "message" && receivedLevel == 2, "callback must receive only the view");
            Check(capture.Text() == "Log [Warning]: message\n", "console and callback receive the same text");
        } },
        { "empty view supplies a valid empty C string", [] {
            LogCapture capture;
            bool validEmpty = false;
            Logger::SetLogCallback([&](int, const char* text) { validEmpty = text && *text == '\0'; });
            Logger::Log(0, {});
            Check(validEmpty, "empty view must not expose a null pointer to native callbacks");
        } },
        { "callback replacement and removal", [] {
            LogCapture capture;
            int first = 0;
            int second = 0;
            Logger::SetLogCallback([&](int, const char*) { ++first; });
            Logger::Log(Logger::LogLevel::Debug, "one");
            Logger::SetLogCallback([&](int, const char*) { ++second; });
            Logger::Log(Logger::LogLevel::Info, "two");
            Logger::SetLogCallback({});
            Logger::Log(Logger::LogLevel::Error, "three");
            Logger::Log(999, "unknown");
            Check(first == 1 && second == 1, "only currently registered callback is invoked");
            Check(capture.Text().find("Log [Unknown]: unknown\n") != std::string::npos, "unknown integer levels");
            Check(capture.Text().find("Log [Error]: three\n") != std::string::npos, "console works without callback");
        } },
        { "concurrent log delivery", [] {
            LogCapture capture;
            std::atomic_int count = 0;
            std::atomic_bool wrong = false;
            Logger::SetLogCallback([&](int level, const char* text) {
                if (level != 1 || std::string_view(text) != "worker") wrong = true;
                ++count;
            });
            std::vector<std::jthread> workers;
            for (int i = 0; i < 4; ++i)
                workers.emplace_back([] { for (int n = 0; n < 100; ++n) Logger::Log(1, "worker"); });
            workers.clear();
            Check(count == 400 && !wrong, "all concurrent messages are delivered intact");
        } },
    });
}
