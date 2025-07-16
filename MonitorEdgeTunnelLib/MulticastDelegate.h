#pragma once

#include <functional>
#include <stdexcept>
#include <vector>
#include <mutex>

// 多播委託 (僅支援C++20以上、執行緒安全)
// 不允許加入重複的函式 (但成員函式帶入不同的實例則視為不重複)
// Invoke會依據加入的順序依次執行所有委託
// 除了原始函數指標沒有物件生命週期的問題外，其他類型的委託都需要確保物件在使用時仍然有效
// 一一一
// Example usage:
//      MulticastDelegate<int, float> delegate;
//      delegate.Add(&Func);
//      delegate.Add(&Class::MemberFunc, &obj, std::placeholders::_1, std::placeholders::_2);
//      delegate.Invoke(42, 3.14f);
//      delegate.Remove(&Func);
//      delegate.Remove(&Class::MemberFunc, &obj);
template<typename... Types>
class MulticastDelegate
{
public:
    // 原始函數指標類型
    using raw_func_ptr_type = void (*)(Types...);

    /// <summary>
    /// 加入委託
    /// <para>使用原始函數指標</para>
    /// <para>----------</para>
    /// <para>範例: delegate.Add(&amp;Func);</para>
    /// </summary>
    /// <param name="fn">原始函數指標</param>
    /// <returns>返回是否加入成功 (false為已存在)</returns>
    bool Add(raw_func_ptr_type fn)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        // create key
        key_type key = make_key(fn, static_cast<void*>(nullptr));

        // create std::function
        delegate_func_type callback(fn);

        // add/replace to container
        return add_callback(std::move(key), std::move(callback));
    }

    /// <summary>
    /// 加入委託
    /// <para>使用成員函數指標和對象指標</para>
    /// <para>支持函式參數的佔位符 (std::placeholders::_1, std::placeholders::_2, ...) 或欲使用的實際參數</para>
    /// <para>注意!! 在對象生命週期結束前務必移除委託</para>
    /// <para>----------</para>
    /// <para>範例1: delegate.Add(&amp;Class::MemberFunc, &amp;obj);</para>
    /// <para>範例2: delegate.Add(&amp;Class::MemberFunc, &amp;obj, std::placeholders::_1);</para>
    /// </summary>
    /// <typeparam name="T">對象類型</typeparam>
    /// <typeparam name="FN">成員函式指標類型</typeparam>
    /// <typeparam name="...Args">參數</typeparam>
    /// <param name="fn">成員函式指標</param>
    /// <param name="t">對象指標</param>
    /// <param name="...args">參數 (optional)</param>
    /// <returns>返回是否加入成功 (false為已存在)</returns>
    template<typename T, typename FN = void (T::*)(Types...), typename... Args>
    bool Add(FN fn, T* t, Args&&... args)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        if (!t)
            throw std::invalid_argument("Null Object Pointer");

        // create key
        key_type key = make_key(fn, t);

        // create std::function
        delegate_func_type callback = make_member_function(fn, t, std::forward<Args>(args)...);

        // add/replace to container
        return add_callback(std::move(key), std::move(callback));
    }

    /// <summary>
    /// 加入委託
    /// <para>使用 std::function 或 lambda閉包函式 或 其他可呼叫物件</para>
    /// <para>注意!! 在物件生命週期結束前務必移除委託</para>
    /// <para>----- 範例 std::function -----</para>
    /// <para>std::function&lt;void(void)&gt; fn = [](){};</para>
    /// <para>delegate.Add(&amp;fn);</para>
    /// <para>----- 範例 lambda closure -----</para>
    /// <para>auto fn = [&amp;](){};</para>
    /// <para>delegate.Add(&amp;fn);</para>
    /// </summary>
    /// <typeparam name="FN">function物件類型</typeparam>
    /// <param name="fn">function物件指標</param>
    /// <returns>返回是否加入成功 (false為已存在)</returns>
    template<typename FN>
    bool Add(const FN* fn) requires
        std::is_class_v<FN> &&                          // 是class
        std::is_invocable_v<FN, Types...> &&            // 可呼叫
        !std::is_convertible_v<FN, raw_func_ptr_type>   // 不能是原始函數指標
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        // create key
        key_type key = make_key(&FN::operator(), fn);

        // create std::function
        delegate_func_type callback(*fn);

        // add/replace to container
        return add_callback(std::move(key), std::move(callback));
    }

    /// <summary>
    /// 移除委託
    /// <para>使用原始函數指標</para>
    /// </summary>
    /// <param name="fn">原始函數指標</param>
    /// <returns>返回是否移除成功 (false為找不到)</returns>
    bool Remove(raw_func_ptr_type fn)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        // create key
        key_type key = make_key(fn, static_cast<void*>(nullptr));

        // remove from container
        return remove_callback(key);
    }

    /// <summary>
    /// 移除委託
    /// <para>使用成員函數指標和對象指標</para>
    /// </summary>
    /// <typeparam name="T">對象類型</typeparam>
    /// <typeparam name="FN">成員函數指標類型</typeparam>
    /// <param name="fn">成員函數指標</param>
    /// <param name="t">對象指標</param>
    /// <returns>返回是否移除成功 (false為找不到)</returns>
    template<typename T, typename FN = void (T::*)(Types...)>
    bool Remove(FN fn, T* t)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        if (!t)
            throw std::invalid_argument("Null Object Pointer");

        // create key
        key_type key = make_key(fn, t);

        // remove from container
        return remove_callback(key);
    }

    /// <summary>
    /// 移除委託
    /// <para>使用 std::function 或 lambda閉包函式 或 其他可呼叫物件</para>
    /// </summary>
    /// <typeparam name="FN">function物件類型</typeparam>
    /// <param name="fn">function物件指標</param>
    /// <returns>返回是否移除成功 (false為找不到)</returns>
    template<typename FN>
    bool Remove(const FN* fn) requires
        std::is_class_v<FN> &&                          // 是class
        std::is_invocable_v<FN, Types...> &&            // 可呼叫
        !std::is_convertible_v<FN, raw_func_ptr_type>   // 不能是原始函數指標
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        // create key
        key_type key = make_key(&FN::operator(), fn);

        // remove from container
        return remove_callback(key);
    }

    // 移除全部委託
    void RemoveAll()
    {
        remove_all_callback();
    }

    // 執行委託
    void Invoke(Types... args)
    {
        invoke_all_callback(std::forward<Types>(args)...);
    }

public: // constructors/destructors/assignment operators
    MulticastDelegate() = default;

    ~MulticastDelegate() = default;

    MulticastDelegate(const MulticastDelegate& obj)
    {
        copy_from(obj);
    }

    MulticastDelegate(MulticastDelegate&& obj)
    {
        move_from(std::move(obj));
    }

    MulticastDelegate& operator=(const MulticastDelegate& obj)
    {
        if (this != &obj)
            copy_from(obj);

        return *this;
    }

    MulticastDelegate& operator=(MulticastDelegate&& obj)
    {
        if (this != &obj)
            move_from(std::move(obj));

        return *this;
    }

private:
    // 定義 delegate function 類型
    using delegate_func_type = std::function<void(Types...)>;

    // 定義 key 類型
    using key_type = std::vector<char>;

    // Helper function to create a key from a function pointer and an object pointer
    template <typename FN, typename U>
    inline key_type make_key(FN fn, U* obj)
    {
        // key data
        struct
        {
            FN fn;
            U* obj;
        } kd = { fn, obj };

        constexpr auto kd_size = sizeof(kd);

        // make binary key
        key_type key(kd_size, 0);
        memcpy(key.data(), &kd, kd_size);

        return key;
    }

    // Helper function to create a member function
    template<typename T, typename FN = void (T::*)(Types...), typename... Args>
    constexpr delegate_func_type make_member_function(FN fn, T* t, Args&&... args)
    {
        constexpr auto args_size = sizeof...(args);

        // if has args, use std::bind to bind the arguments, else use lambda
        if constexpr (args_size)
        {
            return std::bind(fn, t, std::forward<Args>(args)...);
        }
        else
        {
            return  [fn, t](Types... args) {
                        (t->*fn)(std::forward<Types>(args)...);
                    };
        }
    }

    // Helper function to add or replace a callback in the container
    inline bool add_callback(key_type&& key, delegate_func_type&& fn)
    {
        std::lock_guard<std::mutex> lock(mtx);

        for (auto& pair : vecCallback)
        {
            if (pair.first == key)
                return false;
        }

        vecCallback.emplace_back(std::forward<key_type>(key), std::forward<delegate_func_type>(fn));

        return true;
    }

    // Helper function to remove a callback from the container
    inline bool remove_callback(const key_type& key)
    {
        std::lock_guard<std::mutex> lock(mtx);

        for (auto it = vecCallback.begin(); it != vecCallback.end(); ++it)
        {
            if (it->first == key)
            {
                vecCallback.erase(it);
                return true;
            }
        }

        return false;
    }

    // Helper function to remove all callbacks from the container
    inline void remove_all_callback()
    {
        std::lock_guard<std::mutex> lock(mtx);

        vecCallback.clear();
    }

    // Helper function to invoke all callbacks with the provided arguments
    inline void invoke_all_callback(Types... args)
    {
        std::lock_guard<std::mutex> lock(mtx);

        for (const auto& pair : vecCallback)
            pair.second(std::forward<Types>(args)...);
    }

    // copy, used in copy constructor and assignment operator
    inline void copy_from(const MulticastDelegate& obj)
    {
        std::lock_guard<std::mutex> lock(mtx);
        std::lock_guard<std::mutex> lock_obj(obj.mtx);

        // copy something
        vecCallback = obj.vecCallback;
    }

    // move, used in move constructor and assignment operator
    inline void move_from(MulticastDelegate&& obj)
    {
        std::lock_guard<std::mutex> lock(mtx);
        std::lock_guard<std::mutex> lock_obj(obj.mtx);

        // move something
        vecCallback = std::move(obj.vecCallback);
    }

    // vector to store callbacks
    std::vector<std::pair<key_type, delegate_func_type>> vecCallback;

    // mutex for thread safety when using vector
    std::mutex mtx;
};
