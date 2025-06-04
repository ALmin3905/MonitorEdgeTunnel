#pragma once

#include <Windows.h>
#include <string>
#include <string_view>

namespace Utility
{
    /// <summary>
    /// ±N unicode ¦r¦êÂà´«¬° ANSI ¦r¦ê
    /// </summary>
    /// <param name="wstr">unicode¦r¦ê</param>
    /// <returns>ANSI¦r¦ê</returns>
    std::string wchar_to_ansi(const std::wstring_view wstr);

    std::string to_string(const RECT& rc);
}
