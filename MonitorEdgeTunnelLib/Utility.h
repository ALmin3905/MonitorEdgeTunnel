#pragma once

#include <Windows.h>
#include <string>
#include <string_view>

namespace Utility
{
    /// <summary>
    /// �N unicode �r���ഫ�� ANSI �r��
    /// </summary>
    /// <param name="wstr">unicode�r��</param>
    /// <returns>ANSI�r��</returns>
    std::string wchar_to_ansi(const std::wstring_view wstr);

    std::string to_string(const RECT& rc);
}
