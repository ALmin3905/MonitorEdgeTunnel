#pragma once

#include <functional>
#include <stdexcept>
#include <vector>
#include <mutex>

// always_false template to trigger static_assert for unsupported types
template<typename>
constexpr bool always_false = false;

// 是否為 std::pair
template<typename T>
concept is_std_pair = requires { typename T::first_type; typename T::second_type; };

// 是否支援 operator-- (前置遞減)
template<typename T>
concept has_decrement = requires(T it) { { --it } -> std::same_as<T&>; };

// 是否為可使用 operator+=
template<typename T, typename I>
concept has_pluseq = requires(T it, I i) { { it += i } -> std::same_as<T&>; };

// 是否為可使用 operator-=
template<typename T, typename I>
concept has_subeq = requires(T it, I i) { { it -= i } -> std::same_as<T&>; };

// 是否為可使用 operator-= (兩個同類型相減，並返回特定類型)
template<typename T, typename R>
concept has_sub = requires(T it1, T it2) { { it1 - it2 } -> std::same_as<R>; };

// 是否為可使用 operator==
template<typename T>
concept has_eq = requires(T it1, T it2) { { it1 == it2 } -> std::same_as<bool>; };

// 是否為可使用 operator<=> (三路比較)
template<typename T>
concept has_three_way_cmp = requires(T it1, T it2) { { it1 <=> it2 } -> std::same_as<std::strong_ordering>; };

// Primary Template
template<typename T>
class MulticastDelegate
{
    static_assert(always_false<T>, "Wrong Types");
};

// Muticast Delegate Const Iterator (可歷遍取得委託參考)
template<typename _ContainerConstIt>
    requires is_std_pair<typename _ContainerConstIt::value_type>
class _mcdel_const_iterator
{
    // 必須確保 ContainerConstIt 是一個 std::pair 的 Iterator，並且操作第二個元素
    // 然後把 Iterator 必要的操作暴露出來 (使用不支援的操作會編譯錯誤)

public:
    using iterator_category = typename _ContainerConstIt::iterator_category;
    using value_type = typename _ContainerConstIt::value_type::second_type;
    using difference_type = typename _ContainerConstIt::difference_type;
    using pointer = typename const value_type*;
    using reference = typename const value_type&;

    constexpr _mcdel_const_iterator() = default;

    constexpr _mcdel_const_iterator(_ContainerConstIt it) : _it(it) {}

    constexpr reference operator*() const
    {
        return (*_it).second;
    }

    constexpr pointer operator->() const
    {
        return &(*_it).second;
    }

    constexpr _mcdel_const_iterator& operator++()
    {
        ++_it;
        return *this;
    }

    constexpr _mcdel_const_iterator operator++(int)
    {
        _mcdel_const_iterator tmp = *this;
        ++(*this);
        return tmp;
    }

    constexpr _mcdel_const_iterator& operator--()
        requires has_decrement<_ContainerConstIt>
    {
        --_it;
        return *this;
    }

    constexpr _mcdel_const_iterator operator--(int)
        requires has_decrement<_ContainerConstIt>
    {
        _mcdel_const_iterator tmp = *this;
        --(*this);
        return tmp;
    }

    constexpr _mcdel_const_iterator& operator+=(const difference_type offset)
        requires has_pluseq<_ContainerConstIt, difference_type>
    {
        _it += offset;
        return *this;
    }

    constexpr _mcdel_const_iterator operator+(const difference_type offset) const
        requires has_pluseq<_ContainerConstIt, difference_type>
    {
        _mcdel_const_iterator tmp = *this;
        tmp += offset;
        return tmp;
    }

    constexpr _mcdel_const_iterator& operator-=(const difference_type offset)
        requires has_subeq<_ContainerConstIt, difference_type>
    {
        _it -= offset;
        return *this;
    }

    constexpr _mcdel_const_iterator operator-(const difference_type offset) const
        requires has_subeq<_ContainerConstIt, difference_type>
    {
        _mcdel_const_iterator tmp = *this;
        tmp -= offset;
        return tmp;
    }

    constexpr reference operator[](const difference_type offset) const
        requires has_pluseq<_ContainerConstIt, difference_type>
    {
        return (*(_it + offset)).second;
    }

    constexpr difference_type operator-(const _mcdel_const_iterator& other) const
        requires has_sub<_ContainerConstIt, difference_type>
    {
        return _it - other._it;
    }

    constexpr bool operator==(const _mcdel_const_iterator& other) const
        requires has_eq<_ContainerConstIt>
    {
        return _it == other._it;
    }

    constexpr std::strong_ordering operator<=>(const _mcdel_const_iterator& other) const
        requires has_three_way_cmp<_ContainerConstIt>
    {
        return _it <=> other._it;
    }

private:
    _ContainerConstIt _it;

    template<typename T>
    friend class MulticastDelegate;
};

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
template<typename RetType, typename... Types>
class MulticastDelegate<RetType(Types...)>
{
public: // declare types

    // 定義 delegate function 類型
    using delegate_func_type = std::function<RetType(Types...)>;

private: // declare types

    // 定義 key 類型
    using key_type = std::vector<char>;

    // 存放委託容器類型
    using container_type = std::vector<std::pair<key_type, delegate_func_type>>;

    // 是否為 void 返回類型
    static constexpr bool is_void_ret_type = std::is_void_v<RetType>;

public: // declare types

    // 原始函數指標類型
    using raw_func_ptr_type = RetType(*)(Types...);

    // 當執行委託時，返回結果的callback類型
    using invoke_ret_callback_type = std::function<void(RetType)>;

    // const疊代器類型
    using const_iterator = _mcdel_const_iterator<typename container_type::const_iterator>;

    // const疊代器的反向疊代器類型
    using const_reverse_iterator = std::reverse_iterator<const_iterator>;

public: // Modifiers

    /// <summary>
    /// 加入委託
    /// <para>使用原始函數指標</para>
    /// <para>----------</para>
    /// <para>範例: delegate.Add(&amp;Func);</para>
    /// </summary>
    /// <param name="fn">原始函數指標</param>
    /// <returns>返回是否加入成功 (false為已存在)</returns>
    constexpr bool Add(raw_func_ptr_type fn)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        key_type key = make_key(fn, static_cast<void*>(nullptr));
        delegate_func_type callback(fn);

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
    template<typename T, typename FN = RetType(T::*)(Types...), typename... Args>
    constexpr bool Add(FN fn, T* t, Args&&... args)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        if (!t)
            throw std::invalid_argument("Null Object Pointer");

        key_type key = make_key(fn, t);
        delegate_func_type callback = make_member_function(fn, t, std::forward<Args>(args)...);

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
        requires std::is_class_v<FN> &&                         // 是class
                 std::is_invocable_v<FN, Types...> &&           // 可呼叫
                 !std::is_convertible_v<FN, raw_func_ptr_type>  // 不能是原始函數指標
    constexpr bool Add(const FN* fn)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        key_type key = make_key(&FN::operator(), fn);
        delegate_func_type callback(*fn);

        return add_callback(std::move(key), std::move(callback));
    }

    /// <summary>
    /// 插入委託
    /// <para>使用原始函數指標</para>
    /// <para>詳情請參考 Add(...)</para>
    /// </summary>
    /// <param name="it">Iterator 指向欲插入的位置</param>
    /// <param name="fn">原始函數指標</param>
    /// <returns>新插入委託的Iterator (若已存在則返回 end())</returns>
    constexpr const_iterator Insert(const_iterator it, raw_func_ptr_type fn)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        key_type key = make_key(fn, static_cast<void*>(nullptr));
        delegate_func_type callback(fn);

        return insert_callback(it, std::move(key), std::move(callback));
    }

    /// <summary>
    /// 插入委託
    /// <para>使用成員函數指標和對象指標</para>
    /// <para>詳情請參考 Add(...)</para>
    /// </summary>
    /// <param name="it">Iterator 指向欲插入的位置</param>
    /// <param name="fn">成員函式指標</param>
    /// <param name="t">對象指標</param>
    /// <param name="...args">參數 (optional)</param>
    /// <returns>新插入委託的Iterator (若已存在則返回 end())</returns>
    template<typename T, typename FN = RetType(T::*)(Types...), typename... Args>
    constexpr const_iterator Insert(const_iterator it, FN fn, T* t, Args&&... args)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        if (!t)
            throw std::invalid_argument("Null Object Pointer");

        key_type key = make_key(fn, t);
        delegate_func_type callback = make_member_function(fn, t, std::forward<Args>(args)...);

        return insert_callback(it, std::move(key), std::move(callback));
    }

    /// <summary>
    /// 插入委託
    /// <para>使用 std::function 或 lambda閉包函式 或 其他可呼叫物件</para>
    /// <para>詳情請參考 Add(...)</para>
    /// </summary>
    /// <param name="it">Iterator 指向欲插入的位置</param>
    /// <param name="fn">function物件指標</param>
    /// <returns>新插入委託的Iterator (若已存在則返回 end())</returns>
    template<typename FN>
        requires std::is_class_v<FN> &&                         // 是class
                 std::is_invocable_v<FN, Types...> &&           // 可呼叫
                 !std::is_convertible_v<FN, raw_func_ptr_type>  // 不能是原始函數指標
    constexpr const_iterator Insert(const_iterator it, const FN* fn)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        key_type key = make_key(&FN::operator(), fn);
        delegate_func_type callback(*fn);

        return insert_callback(it, std::move(key), std::move(callback));
    }

    /// <summary>
    /// 移除委託
    /// <para>使用原始函數指標</para>
    /// </summary>
    /// <param name="fn">原始函數指標</param>
    /// <returns>返回是否移除成功 (false為找不到)</returns>
    constexpr bool Remove(raw_func_ptr_type fn)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        key_type key = make_key(fn, static_cast<void*>(nullptr));

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
    template<typename T, typename FN = RetType(T::*)(Types...)>
    constexpr bool Remove(FN fn, T* t)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        if (!t)
            throw std::invalid_argument("Null Object Pointer");

        key_type key = make_key(fn, t);

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
        requires std::is_class_v<FN> &&                         // 是class
                 std::is_invocable_v<FN, Types...> &&           // 可呼叫
                 !std::is_convertible_v<FN, raw_func_ptr_type>  // 不能是原始函數指標
    constexpr bool Remove(const FN* fn)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        key_type key = make_key(&FN::operator(), fn);

        return remove_callback(key);
    }

    /// <summary>
    /// 移除委託
    /// <para>使用 const_iterator</para>
    /// <para>傳入無效的 Iterator 為未定義行為，會觸發嚴重錯誤</para>
    /// </summary>
    /// <returns>被刪除委託的下一個Iterator (如果被刪除的委託是最後一個則返回 end())</returns>
    /// <exception cref="any">容器配置操錯時的錯誤</exception>
    constexpr const_iterator Remove(const_iterator it)
    {
        return remove_callback(it);
    }

    /// <summary>
    /// 移除委託 (範圍刪除)
    /// <para>使用 const_iterator</para>
    /// <para>傳入無效的 Iterator 為未定義行為，會觸發嚴重錯誤</para>
    /// </summary>
    /// <returns>被刪除委託的下一個Iterator。如果 last == end() 則返回 end()。如果 [first, last] 為空範圍則返回 last</returns>
    /// <exception cref="any">容器配置操錯時的錯誤</exception>
    constexpr const_iterator Remove(const_iterator first, const_iterator last)
    {
        return remove_callback(first, last);
    }

    /// <summary>
    /// 移除全部委託
    /// </summary>
    constexpr void RemoveAll()
    {
        remove_all_callback();
    }

    /// <summary>
    /// 結合委託至當前委託
    /// <para>會忽略重複的委託</para>
    /// </summary>
    constexpr MulticastDelegate& Combine(const MulticastDelegate& obj)
    {
        combine(obj);

        return *this;
    }

public: // Operations

    /// <summary>
    /// 執行委託
    /// <para>執行期間會複製一份委託清單獨立執行，因此不會阻塞其他執行緒的操作</para>
    /// </summary>
    /// <param name="...args">委託函式的參數</param>
    /// <returns>返回最後一個委託執行的結果</returns>
    /// <exception cref="std::runtime_error">沒有任何委託可執行(返回類型為void則不會觸發例外)</exception>
    /// <exception cref="other">委託函式內部拋的例外</exception>
    constexpr RetType Invoke(Types... args) const
    {
        if constexpr (is_void_ret_type)
            invoke_all_callback(std::forward<Types>(args)...);
        else
            return invoke_all_callback(std::forward<Types>(args)...);
    }

    /// <summary>
    /// 執行委託 (返回類型為void時不可呼叫)
    /// <para>執行期間會複製一份委託清單獨立執行，因此不會阻塞其他執行緒的操作</para>
    /// </summary>
    /// <param name="...args">委託函式的參數</param>
    /// <param name="fn">返回委託執行結果的callback</param>
    /// <exception cref="any">委託函式內部拋的例外</exception>
    constexpr void Invoke(Types... args, const invoke_ret_callback_type& fn) const
        requires (!is_void_ret_type)
    {
        invoke_all_callback(std::forward<Types>(args)..., fn);
    }

public: // Accessors

    /// <summary>
    /// 取得委託參考
    /// </summary>
    /// <param name="pos">位置</param>
    /// <exception cref="std::out_of_range">位置超出範圍</exception>
    constexpr const delegate_func_type& At(size_t pos) const
    {
        return at(pos);
    }

    /// <summary>
    /// 找到委託 (Iterator)
    /// <para>使用原始函數指標</para>
    /// </summary>
    /// <returns>找不到則返回 end()</returns>
    constexpr const_iterator Find(raw_func_ptr_type fn) const
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        key_type key = make_key(fn, static_cast<void*>(nullptr));

        return find(key);
    }

    /// <summary>
    /// 找到委託 (Iterator)
    /// <para>使用成員函數指標和對象指標</para>
    /// </summary>
    /// <returns>找不到則返回 end()</returns>
    template<typename T, typename FN = RetType(T::*)(Types...)>
    constexpr const_iterator Find(FN fn, T* t) const
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        if (!t)
            throw std::invalid_argument("Null Object Pointer");

        key_type key = make_key(fn, t);

        return find(key);
    }

    /// <summary>
    /// 找到委託 (Iterator)
    /// <para>使用 std::function 或 lambda閉包函式 或 其他可呼叫物件</para>
    /// </summary>
    /// <returns>找不到則返回 end()</returns>
    template<typename FN>
        requires std::is_class_v<FN> &&                         // 是class
                 std::is_invocable_v<FN, Types...> &&           // 可呼叫
                 !std::is_convertible_v<FN, raw_func_ptr_type>  // 不能是原始函數指標
    constexpr const_iterator Find(const FN* fn) const
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        key_type key = make_key(&FN::operator(), fn);

        return find(key);
    }

public: // Capacity

    /// <summary>
    /// 是否為空委託
    /// </summary>
    constexpr bool Empty() const
    {
        return empty();
    }

    /// <summary>
    /// 委託數量
    /// </summary>
    constexpr size_t Size() const
    {
        return size();
    }

public: // Comparisons

    /// <summary>
    /// 是否相等
    /// </summary>
    constexpr bool Equals(const MulticastDelegate& obj) const
    {
        return equals(obj);
    }

    /// <summary>
    /// 是否存在委託
    /// <para>使用原始函數指標</para>
    /// </summary>
    constexpr bool Exists(raw_func_ptr_type fn)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        key_type key = make_key(fn, static_cast<void*>(nullptr));

        return exists(key);
    }

    /// <summary>
    /// 是否存在委託
    /// <para>使用成員函數指標和對象指標</para>
    /// </summary>
    template<typename T, typename FN = RetType(T::*)(Types...)>
    constexpr bool Exists(FN fn, T* t)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        if (!t)
            throw std::invalid_argument("Null Object Pointer");

        key_type key = make_key(fn, t);

        return exists(key);
    }

    /// <summary>
    /// 是否存在委託
    /// <para>使用 std::function 或 lambda閉包函式 或 其他可呼叫物件</para>
    /// </summary>
    template<typename FN>
        requires std::is_class_v<FN> &&                         // 是class
                 std::is_invocable_v<FN, Types...> &&           // 可呼叫
                 !std::is_convertible_v<FN, raw_func_ptr_type>  // 不能是原始函數指標
    constexpr bool Exists(const FN* fn)
    {
        if (!fn)
            throw std::invalid_argument("Null Function Pointer");

        key_type key = make_key(&FN::operator(), fn);

        return exists(key);
    }

public: // Iterators

    /// <summary>
    /// Iterator Begin
    /// <para>可取得委託參考</para>
    /// <para>雖然呼叫時為執行緒安全，但委託增減可能導致Iterator失效，因此若有多執行緒共用時，應自行在外部上鎖以保證歷遍Iterator時委託不會有變化</para>
    /// </summary>
    constexpr const_iterator begin() const
    {
        return _begin();
    }

    /// <summary>
    /// Iterator End
    /// <para>可取得委託參考</para>
    /// <para>雖然呼叫時為執行緒安全，但委託增減可能導致Iterator失效，因此若有多執行緒共用時，應自行在外部上鎖以保證歷遍Iterator時委託不會有變化</para>
    /// </summary>
    constexpr const_iterator end() const
    {
        return _end();
    }

    /// <summary>
    /// Reverse Iterator Begin
    /// <para>可取得委託參考</para>
    /// <para>雖然呼叫時為執行緒安全，但委託增減可能導致Iterator失效，因此若有多執行緒共用時，應自行在外部上鎖以保證歷遍Iterator時委託不會有變化</para>
    /// </summary>
    constexpr const_reverse_iterator rbegin() const
    {
        return _rbegin();
    }

    /// <summary>
    /// Reverse Iterator End
    /// <para>可取得委託參考</para>
    /// <para>雖然呼叫時為執行緒安全，但委託增減可能導致Iterator失效，因此若有多執行緒共用時，應自行在外部上鎖以保證歷遍Iterator時委託不會有變化</para>
    /// </summary>
    constexpr const_reverse_iterator rend() const
    {
        return _rend();
    }

public: // operator overloads

    // 呼叫運算子，直接執行委託
    constexpr RetType operator()(Types... args) const
    {
        if constexpr (is_void_ret_type)
            Invoke(std::forward<Types>(args)...);
        else
            return Invoke(std::forward<Types>(args)...);
    }

    // 呼叫運算子，執行委託並傳入返回結果的callback
    constexpr void operator()(Types... args, const invoke_ret_callback_type& fn) const
        requires (!is_void_ret_type)
    {
        Invoke(std::forward<Types>(args)..., fn);
    }

    // 訪問運算子，取得委託在指定位置的參考
    constexpr const delegate_func_type& operator[](size_t pos) const
    {
        return At(pos);
    }

    // 結合運算子，將另一個委託結合到當前委託
    constexpr MulticastDelegate operator+(const MulticastDelegate& obj) const
    {
        MulticastDelegate result(*this);
        result.Combine(obj);
        return result;
    }

    // 結合運算子，將另一個委託結合到當前委託 (修改當前委託)
    constexpr MulticastDelegate& operator+=(const MulticastDelegate& obj)
    {
        return Combine(obj);
    }

    // 比較運算子，檢查兩個委託是否相等
    constexpr bool operator==(const MulticastDelegate& obj) const
    {
        return Equals(obj);
    }

    // (bool)轉換運算子，檢查委託是否為空
    constexpr operator bool() const
    {
        return !Empty();
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

private: // Private helper functions

    // Helper function to create a key from a function pointer and an object pointer
    template <typename FN, typename U>
    constexpr key_type make_key(FN fn, U* obj) const
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
    template<typename T, typename FN = RetType(T::*)(Types...), typename... Args>
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
            return  [fn, t](Types... args)
                    {
                        if constexpr (is_void_ret_type)
                            (t->*fn)(std::forward<Types>(args)...);
                        else
                            return (t->*fn)(std::forward<Types>(args)...);
                    };
        }
    }

    // Helper function to add a callback in the container (no lock inside)
    constexpr bool add_callback_locked(key_type&& key, delegate_func_type&& fn)
    {
        for (auto& pair : vecCallback)
        {
            if (pair.first == key)
                return false;
        }

        vecCallback.emplace_back(std::forward<key_type>(key), std::forward<delegate_func_type>(fn));

        return true;
    }

    // Helper function to add a callback in the container
    constexpr bool add_callback(key_type&& key, delegate_func_type&& fn)
    {
        std::lock_guard<std::mutex> lock(mtx);

        return add_callback_locked(std::forward<key_type>(key), std::forward<delegate_func_type>(fn));
    }

    // Helper function to insert a callback at a specific position
    constexpr const_iterator insert_callback(const_iterator it, const key_type& key, const delegate_func_type& fn)
    {
        std::lock_guard<std::mutex> lock(mtx);

        for (auto& pair : vecCallback)
        {
            if (pair.first == key)
                return const_iterator(vecCallback.end());
        }

        auto tmp = vecCallback.insert(it._it, std::make_pair(key, fn));
        return const_iterator(tmp);
    }

    // Helper function to remove a callback from the container
    constexpr bool remove_callback(const key_type& key)
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

    // Helper function to remove a callback using const_iterator
    constexpr const_iterator remove_callback(const_iterator it)
    {
        std::lock_guard<std::mutex> lock(mtx);

        return const_iterator(vecCallback.erase(it._it));
    }

    // Helper function to remove a range of callbacks using const_iterator
    constexpr const_iterator remove_callback(const_iterator first, const_iterator last)
    {
        std::lock_guard<std::mutex> lock(mtx);

        return const_iterator(vecCallback.erase(first._it, last._it));
    }

    // Helper function to remove all callbacks from the container
    constexpr void remove_all_callback()
    {
        std::lock_guard<std::mutex> lock(mtx);

        vecCallback.clear();
    }

    // Helper function to get copy of the callback vector
    constexpr std::vector<delegate_func_type> get_copy_callbacks() const
    {
        std::lock_guard<std::mutex> lock(mtx);

        std::vector<delegate_func_type> vecCopy;
        vecCopy.reserve(vecCallback.size());

        for (const auto& pair : vecCallback)
        {
            vecCopy.push_back(pair.second);
        }

        return vecCopy;
    }

    // Helper function to invoke all callbacks with the provided arguments
    constexpr RetType invoke_all_callback(Types... args) const
    {
        auto vecCopyCallback = get_copy_callbacks();

        if constexpr (is_void_ret_type)
        {
            for (const auto& callback : vecCopyCallback)
                callback(std::forward<Types>(args)...);
        }
        else
        {
            if (vecCopyCallback.empty())
                throw std::runtime_error("No callbacks registered.");

            // foreach callback except the last one
            for (int i = 0; i < static_cast<int>(vecCopyCallback.size()) - 1; ++i)
                vecCopyCallback[i](std::forward<Types>(args)...);

            return vecCopyCallback.back()(std::forward<Types>(args)...);
        }
    }

    // Helper function to invoke all callbacks with the provided arguments and a callback for the return value
    constexpr void invoke_all_callback(Types... args, const invoke_ret_callback_type& fn) const
        requires (!is_void_ret_type)
    {
        auto vecCopyCallback = get_copy_callbacks();

        for (const auto& callback : vecCopyCallback)
            fn(callback(std::forward<Types>(args)...));
    }

    // Helper function to combine another MulticastDelegate into this one
    constexpr void combine(const MulticastDelegate& obj)
    {
        std::scoped_lock lock(mtx, obj.mtx);

        // combine the callback vectors, avoiding duplicates
        for (const auto& pair : obj.vecCallback)
            add_callback_locked(pair.first, pair.second);
    }

    // Helper function to check if a callback exists in the container
    constexpr bool exists(const key_type& key) const
    {
        std::lock_guard<std::mutex> lock(mtx);

        for (const auto& pair : vecCallback)
        {
            if (pair.first == key)
                return true;
        }

        return false;
    }

    // Helper function to get a callback at a specific position
    constexpr const delegate_func_type& at(size_t pos) const
    {
        std::lock_guard<std::mutex> lock(mtx);

        return vecCallback.at(pos).second;
    }

    // Helper function to find a callback by key
    constexpr const_iterator find(const key_type& key) const
    {
        std::lock_guard<std::mutex> lock(mtx);

        auto it = std::find_if(vecCallback.begin(), vecCallback.end(),
            [&key](const auto& pair) { return pair.first == key; });

        return const_iterator(it);
    }

    // Helper function to check if there are any callbacks registered
    constexpr bool empty() const
    {
        std::lock_guard<std::mutex> lock(mtx);

        return vecCallback.empty();
    }

    // Helper function to get the size of the callback vector
    constexpr size_t size() const
    {
        std::lock_guard<std::mutex> lock(mtx);

        return vecCallback.size();
    }

    // Helper function to check if two MulticastDelegate objects are equal
    constexpr bool equals(const MulticastDelegate& obj) const
    {
        std::scoped_lock lock(mtx, obj.mtx);

        if (vecCallback.size() != obj.vecCallback.size())
            return false;

        for (size_t i = 0; i < vecCallback.size(); ++i)
        {
            // check if keys are equal
            if (vecCallback[i].first != obj.vecCallback[i].first)
                return false;
        }

        return true;
    }

    // Helper functions to get const iterators for the callback vector
    constexpr const_iterator _begin() const
    {
        std::lock_guard<std::mutex> lock(mtx);

        return const_iterator(vecCallback.begin());
    }

    // Helper function to get the end const iterator for the callback vector
    constexpr const_iterator _end() const
    {
        std::lock_guard<std::mutex> lock(mtx);

        return const_iterator(vecCallback.end());
    }

    // Helper functions to get const reverse iterators for the callback vector
    constexpr const_reverse_iterator _rbegin() const
    {
        std::lock_guard<std::mutex> lock(mtx);

        return const_reverse_iterator(vecCallback.rbegin());
    }

    // Helper function to get the end const reverse iterator for the callback vector
    constexpr const_reverse_iterator _rend() const
    {
        std::lock_guard<std::mutex> lock(mtx);

        return const_reverse_iterator(vecCallback.rend());
    }

    // copy, used in copy constructor and assignment operator
    constexpr void copy_from(const MulticastDelegate& obj)
    {
        std::scoped_lock lock(mtx, obj.mtx);

        vecCallback = obj.vecCallback;
    }

    // move, used in move constructor and assignment operator
    constexpr void move_from(MulticastDelegate&& obj)
    {
        std::scoped_lock lock(mtx, obj.mtx);

        vecCallback = std::move(obj.vecCallback);
    }

private: // Member variables

    // vector to store callbacks
    container_type vecCallback;

    // mutex for thread safety when using vector
    mutable std::mutex mtx;
};
