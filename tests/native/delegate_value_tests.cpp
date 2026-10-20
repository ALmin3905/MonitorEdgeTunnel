#include <algorithm>
#include <cstring>
#include <memory>
#include "MulticastDelegate.h"
#include "TestSupport.h"

int main()
{
    return RunTests({
        { "each void subscriber receives the complete value", [] {
            MulticastDelegate<void(std::string)> delegate;
            std::vector<std::string> received;
            auto first = [&](std::string text) { received.push_back(std::move(text)); };
            auto second = [&](std::string text) { received.push_back(std::move(text)); };
            delegate.Add(&first);
            delegate.Add(&second);
            const std::string input(100, 'x');
            delegate(input);
            Check(received == std::vector<std::string>{ input, input }, "earlier subscriber must not consume later input");
            Check(input.size() == 100, "caller's lvalue retained");
        } },
        { "return and collector overloads preserve values", [] {
            MulticastDelegate<size_t(std::string)> delegate;
            auto first = [bias = 1](std::string text) { return text.size() + bias; };
            auto second = [bias = 2](std::string text) { return text.size() + bias; };
            delegate.Add(&first);
            delegate.Add(&second);
            Check(delegate.Invoke(std::string(100, 'x')) == 102, "last subscriber receives full input");
            std::vector<size_t> results;
            delegate.Invoke(std::string(100, 'x'), [&](size_t result) { results.push_back(result); });
            Check(results == std::vector<size_t>{ 101, 102 }, "collector invocation receives full input each time");
        } },
        { "explicit rvalue references and move-only values remain supported", [] {
            MulticastDelegate<void(std::unique_ptr<int>)> ownership;
            int received = 0;
            auto consume = [&](std::unique_ptr<int> value) { received = *value; };
            ownership.Add(&consume);
            ownership(std::make_unique<int>(42));
            Check(received == 42, "single move-only subscriber can take ownership");
            MulticastDelegate<void(std::string&&)> forwarding;
            auto move = [&](std::string&& value) { value += '!'; };
            forwarding.Add(&move);
            std::string text = "reference";
            forwarding(std::move(text));
            Check(text == "reference!", "explicit rvalue reference identity retained");
        } },
    });
}
