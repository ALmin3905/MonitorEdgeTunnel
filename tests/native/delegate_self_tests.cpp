#include <algorithm>
#include <cstring>
#include "MulticastDelegate.h"
#include "TestSupport.h"

int main()
{
    return RunTests({
        { "self equality and combination", [] {
            MulticastDelegate<void()> delegate;
            Check(delegate.Equals(delegate) && delegate == delegate, "empty self equality");
            delegate.Combine(delegate);
            Check(delegate.Empty(), "combining an empty delegate with itself is a no-op");
            int calls = 0;
            auto callback = [&] { ++calls; };
            delegate.Add(&callback);
            Check(delegate.Equals(delegate), "nonempty self equality");
            delegate += delegate;
            delegate.Invoke();
            Check(delegate.Size() == 1 && calls == 1, "self combination does not duplicate subscriptions");
        } },
    });
}
