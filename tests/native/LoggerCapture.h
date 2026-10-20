#pragma once

#include "Logger.h"
#include <iostream>
#include <mutex>
#include <streambuf>
#include <string>

// Capture console logs safely even when several test threads log concurrently.
class LogCapture : public std::streambuf
{
public:
    LogCapture() : m_previous(std::cout.rdbuf(this)) { Logger::SetLogCallback({}); }
    ~LogCapture() { Logger::SetLogCallback({}); std::cout.rdbuf(m_previous); }

    std::string Text()
    {
        std::lock_guard lock(m_mutex);
        return m_text;
    }

protected:
    std::streamsize xsputn(const char* text, std::streamsize count) override
    {
        std::lock_guard lock(m_mutex);
        m_text.append(text, static_cast<size_t>(count));
        return count;
    }

    int_type overflow(int_type value) override
    {
        if (!traits_type::eq_int_type(value, traits_type::eof()))
        {
            const char character = traits_type::to_char_type(value);
            xsputn(&character, 1);
        }
        return traits_type::not_eof(value);
    }

private:
    std::streambuf* m_previous;
    std::mutex m_mutex;
    std::string m_text;
};
