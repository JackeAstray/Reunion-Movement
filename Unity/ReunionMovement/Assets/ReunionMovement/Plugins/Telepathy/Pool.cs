// 用于避免分配的对象池。最初来自 libuv2k。
using System;
using System.Collections.Generic;

namespace Telepathy
{
    public class Pool<T>
    {
        // 对象栈
        readonly Stack<T> objects = new Stack<T>();

        // 某些类型的构造函数可能需要额外参数，因此使用 Func<T> 生成器
        readonly Func<T> objectGenerator;

        // 构造函数
        public Pool(Func<T> objectGenerator)
        {
            this.objectGenerator = objectGenerator;
        }

        // 从池中取出元素，如果为空则创建一个新的
        public T Take() => objects.Count > 0 ? objects.Pop() : objectGenerator();

        // 将元素返回到池中
        public void Return(T item) => objects.Push(item);

        // 清空池并对每个对象应用 disposer 函数（此处无需额外处理）
        public void Clear() => objects.Clear();

        // 返回池中对象数量。用于测试。
        public int Count() => objects.Count;
    }
}