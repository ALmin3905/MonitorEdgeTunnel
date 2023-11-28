#pragma once

#include <vector>
#include <string>

/// <summary>
/// base64�s�X
/// </summary>
/// <param name="buf">�G�i����</param>
/// <param name="bufLen">��ƪ���</param>
/// <returns>�s�X�r��</returns>
std::string base64_encode(const unsigned char* buf, unsigned int bufLen);

/// <summary>
/// base64�ѽX
/// </summary>
/// <param name="encoded_string">�s�X�r��</param>
/// <returns>�ѽX�G�i����</returns>
std::vector<unsigned char> base64_decode(const std::string& encoded_string);