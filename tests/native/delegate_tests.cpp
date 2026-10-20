#include <algorithm>
#include <cstring>
#include "MulticastDelegate.h"
#include "TestSupport.h"
#include <atomic>

namespace
{
    int AddOne(int value) { return value + 1; }
    int Double(int value) { return value * 2; }

    // Separate object identities must remain distinct subscriptions to the same method.
    struct Receiver
    {
        int offset = 0;
        int Add(int value) { return offset + value; }
        int Sum(int left, int right) { return offset + left + right; }
        int Read(int value) const { return offset + value; }
    };
}

int main()
{
    return RunTests({
        { "empty delegates and return policy", [] {
            MulticastDelegate<void()> action;
            action.Invoke();
            action();
            Check(action.Empty() && action.Size() == 0 && !action, "empty capacity and boolean conversion");
            MulticastDelegate<int(int)> query;
            CheckThrows<std::runtime_error>([&] { query.Invoke(1); }, "empty non-void invoke throws");
            int delivered = 0;
            query.Invoke(1, [&](int) { ++delivered; });
            Check(delivered == 0, "empty result collector receives nothing");
        } },
        { "raw subscriptions, identity, and result order", [] {
            MulticastDelegate<int(int)> delegate;
            Check(delegate.Add(&AddOne) && !delegate.Add(&AddOne), "duplicate raw function rejected");
            Check(delegate.Add(&Double) && delegate.Exists(&Double), "second function added");
            Check(delegate.Invoke(3) == 6 && delegate(4) == 8, "last subscriber result returned");
            std::vector<int> results;
            delegate(3, [&](int result) { results.push_back(result); });
            Check(results == std::vector<int>{ 4, 6 }, "collector preserves subscription order");
            Check(delegate.Remove(&AddOne) && !delegate.Remove(&AddOne), "remove reports membership");
            Check(delegate.Find(&AddOne) == delegate.end(), "removed function not found");
            delegate.RemoveAll();
            Check(delegate.Empty(), "remove all");
        } },
        { "member methods and bound arguments", [] {
            Receiver first{ 10 }, second{ 20 };
            const Receiver readOnly{ 30 };
            MulticastDelegate<int(int)> delegate;
            Check(delegate.Add(&Receiver::Add, &first), "member added");
            Check(!delegate.Add(&Receiver::Add, &first), "same member and object duplicate");
            Check(delegate.Add(&Receiver::Add, &second), "different object is independent");
            Check(delegate.Add(&Receiver::Read, &readOnly), "const member supported");
            Check(delegate.Add(&Receiver::Sum, &first, std::placeholders::_1, 7), "bound parameter supported");
            Check(delegate.Invoke(2) == 19, "bound argument used");
            Check(delegate.Find(&Receiver::Add, &first) == delegate.begin(), "find member identity");
            Check(delegate.Exists(&Receiver::Add, &second), "member exists");
            Check(delegate.Remove(&Receiver::Add, &second), "remove one object only");
            Check(delegate.Exists(&Receiver::Add, &first), "other object stays registered");
        } },
        { "callable object subscriptions", [] {
            int count = 0;
            auto callback = [&](int value) { count += value; return count; };
            std::function<int(int)> function = [](int value) { return value * 10; };
            MulticastDelegate<int(int)> delegate;
            Check(delegate.Add(&callback) && !delegate.Add(&callback), "lambda identity de-duplicated");
            Check(delegate.Add(&function), "std::function supported");
            Check(delegate.Exists(&callback) && delegate.Find(&function) != delegate.end(), "callable lookup");
            Check(delegate.Invoke(2) == 20 && count == 2, "all callable types invoked");
            Check(delegate.Remove(&callback) && !delegate.Remove(&callback), "lambda removal");
            Check(delegate.Remove(&function) && delegate.Empty(), "std::function removal");
        } },
        { "insert, random access, and erase iterators", [] {
            MulticastDelegate<int(int)> delegate;
            delegate.Add(&Double);
            Check(delegate.Insert(delegate.begin(), &AddOne) == delegate.begin(), "insert at beginning");
            Check(delegate.Insert(delegate.end(), &AddOne) == delegate.end(), "duplicate insert returns end");
            auto iterator = delegate.begin();
            Check((*iterator)(3) == 4 && iterator->operator()(3) == 4, "iterator reference and pointer");
            Check(iterator[1](3) == 6 && delegate[1](3) == 6 && delegate.At(0)(3) == 4, "indexed access");
            Check(delegate.end() - iterator == 2 && iterator < delegate.end(), "iterator distance and ordering");
            Check((*(iterator++))(3) == 4, "postincrement");
            Check((*iterator)(3) == 6, "increment advances");
            --iterator;
            iterator += 2;
            iterator -= 1;
            Check((*iterator)(3) == 6 && (iterator - 1) == delegate.begin(), "iterator arithmetic");
            Check((*delegate.rbegin())(3) == 6 && delegate.rend() - delegate.rbegin() == 2, "reverse traversal");
            CheckThrows<std::out_of_range>([&] { delegate.At(2); }, "out of range access");
            Check(delegate.Remove(delegate.begin()) == delegate.begin() && delegate.Size() == 1, "erase iterator");
            delegate.Remove(delegate.begin(), delegate.end());
            Check(delegate.Empty(), "erase range");
        } },
        { "insert callable and member at explicit positions", [] {
            Receiver object{ 10 };
            auto callable = [bias = 3](int value) { return value + bias; };
            MulticastDelegate<int(int)> delegate;
            delegate.Insert(delegate.end(), &Receiver::Add, &object);
            delegate.Insert(delegate.begin(), &callable);
            Check(delegate.Invoke(1) == 11 && delegate.At(0)(1) == 4, "all insert overloads preserve position");
        } },
        { "copy and move retain independent lists", [] {
            MulticastDelegate<int(int)> original;
            original.Add(&AddOne);
            MulticastDelegate<int(int)> copied(original);
            Check(copied == original, "copy equality");
            copied.Add(&Double);
            Check(!copied.Equals(original) && original.Size() == 1, "copy list independent");
            MulticastDelegate<int(int)> assigned;
            assigned = copied;
            assigned = assigned;
            Check(assigned == copied, "copy assignment and self-assignment");
            MulticastDelegate<int(int)> moved(std::move(copied));
            Check(moved(3) == 6, "move construction");
            assigned = std::move(moved);
            assigned = std::move(assigned);
            Check(assigned(3) == 6, "move assignment and self-move");
        } },
        { "callback changes affect the next invocation", [] {
            MulticastDelegate<void()> delegate;
            int first = 0, second = 0;
            auto later = [&] { ++second; };
            auto replace = [&] { ++first; delegate.RemoveAll(); delegate.Add(&later); };
            delegate.Add(&replace);
            delegate.Invoke();
            Check(first == 1 && second == 0, "snapshot remains stable during modification");
            delegate.Invoke();
            Check(first == 1 && second == 1, "next invocation uses updated list");
        } },
        { "reference arguments preserve identity and mutations", [] {
            MulticastDelegate<void(std::string&)> delegate;
            auto first = [](std::string& text) { text += 'a'; };
            auto second = [](std::string& text) { text += 'b'; };
            delegate.Add(+first);
            delegate.Add(+second);
            std::string text;
            delegate(text);
            Check(text == "ab", "references are shared in invocation order");
        } },
        { "subscriber exceptions propagate and stop dispatch", [] {
            MulticastDelegate<void()> delegate;
            bool laterCalled = false;
            auto fail = [] { throw std::logic_error("callback"); };
            auto later = [&] { laterCalled = true; };
            delegate.Add(+fail);
            delegate.Add(&later);
            CheckThrows<std::logic_error>([&] { delegate(); }, "callback error propagated");
            Check(!laterCalled, "dispatch stops at failing subscriber");
        } },
        { "null callback inputs rejected", [] {
            MulticastDelegate<int(int)> delegate;
            using Raw = decltype(delegate)::raw_func_ptr_type;
            Receiver object;
            auto callback = [bias = 1](int value) { return value + bias; };
            using Callable = decltype(callback);
            CheckThrows<std::invalid_argument>([&] { delegate.Add(static_cast<Raw>(nullptr)); }, "null raw add");
            CheckThrows<std::invalid_argument>([&] { delegate.Remove(static_cast<Raw>(nullptr)); }, "null raw remove");
            CheckThrows<std::invalid_argument>([&] { delegate.Find(static_cast<Raw>(nullptr)); }, "null raw find");
            CheckThrows<std::invalid_argument>([&] { delegate.Exists(static_cast<Raw>(nullptr)); }, "null raw exists");
            CheckThrows<std::invalid_argument>([&] { delegate.Add(&Receiver::Add, static_cast<Receiver*>(nullptr)); }, "null object");
            CheckThrows<std::invalid_argument>([&] { delegate.Add(static_cast<int(Receiver::*)(int)>(nullptr), &object); }, "null member");
            CheckThrows<std::invalid_argument>([&] { delegate.Add(static_cast<Callable*>(nullptr)); }, "null callable");
            CheckThrows<std::invalid_argument>([&] { delegate.Insert(delegate.end(), static_cast<Raw>(nullptr)); }, "null insertion");
        } },
        { "concurrent subscription changes and dispatch", [] {
            MulticastDelegate<void()> delegate;
            std::atomic_int called = 0;
            auto callback = [&] { ++called; };
            std::jthread modifier([&] {
                for (int i = 0; i < 1000; ++i) { delegate.Add(&callback); delegate.Remove(&callback); }
            });
            for (int i = 0; i < 1000; ++i) delegate();
            modifier.join();
            delegate.Add(&callback);
            const int before = called;
            delegate();
            Check(called == before + 1, "delegate remains usable after concurrent changes");
        } },
    });
}
