#include <algorithm>
#include <cstring>
#include "MulticastDelegate.h"
#include "TestSupport.h"

int main()
{
    return RunTests({
        { "combination preserves order and ignores duplicates", [] {
            auto first = [bias = 1](int value) { return value + bias; };
            auto second = [bias = 2](int value) { return value + bias; };
            MulticastDelegate<int(int)> left, right;
            left.Add(&first);
            right.Add(&first);
            right.Add(&second);
            auto combined = left + right;
            Check(combined.Size() == 2 && combined(10) == 12, "operator+ combines in order");
            Check(left.Size() == 1 && right.Size() == 2, "operator+ leaves sources independent");
            left += right;
            Check(left == combined, "operator+= combines in place");
            left.Combine(right);
            Check(left.Size() == 2, "repeated combination de-duplicates");
        } },
    });
}
