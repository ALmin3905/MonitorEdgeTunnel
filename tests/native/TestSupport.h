#pragma once

#include <chrono>
#include <exception>
#include <functional>
#include <initializer_list>
#include <iostream>
#include <stdexcept>
#include <string>
#include <thread>
#include <utility>

// Reject a failed behavior assertion with a diagnostic reported by the suite.
inline void Check(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

// Require the requested exception type; an unexpected exception remains a failure.
template<typename Exception, typename Function>
void CheckThrows(Function&& function, const char* message)
{
    try
    {
        function();
    }
    catch (const Exception&)
    {
        return;
    }
    throw std::runtime_error(message);
}

// Bound asynchronous waits so a missing signal reports a failure instead of hanging.
template<typename Predicate>
bool WaitUntil(Predicate&& predicate, std::chrono::milliseconds timeout = std::chrono::seconds(3))
{
    const auto deadline = std::chrono::steady_clock::now() + timeout;
    while (!predicate())
    {
        if (std::chrono::steady_clock::now() >= deadline)
            return false;
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    }
    return true;
}

// Run independent named cases, print every failure, and return a process exit status.
inline int RunTests(std::initializer_list<std::pair<const char*, std::function<void()>>> tests)
{
    int failed = 0;
    for (const auto& [name, test] : tests)
    {
        try
        {
            test();
            std::cout << "  PASS " << name << '\n';
        }
        catch (const std::exception& error)
        {
            ++failed;
            std::cerr << "  FAIL " << name << ": " << error.what() << '\n';
        }
    }
    std::cout << tests.size() - failed << '/' << tests.size() << " cases passed\n";
    return failed == 0 ? 0 : 1;
}
