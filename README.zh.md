# Aurora MemoryPack

![许可](https://img.shields.io/github/license/NOMOAX/aurora.memorypack)
![版本](https://img.shields.io/badge/version-1.0.4-blue)
![最低 Unity 版本](https://img.shields.io/badge/Unity-2021.2%2B-blue)

为 MemoryPack 补充版本迁移能力的配套工具包。

[English](README.md) | 中文

## 依赖

- [Aurora](https://github.com/NOMOAX/aurora.git)
- [MemoryPack](https://github.com/Cysharp/MemoryPack)

## 安装

1. 打开 Unity package manager。
2. 点击左上角的 `+` 按钮，然后选择 `Add package from git URL...`。
3. 填入 `https://github.com/NOMOAX/aurora.memorypack.git` 并点击 `Add` 按钮。

## 版本迁移

MemoryPack 把联合类型（添加了 `[MemoryPackUnion]` 特性的接口或抽象类）序列化为"具体类型的联合标签 + 该具体类型的成员"，因此读取方必须仍然认识所有可能被写入过的具体类型。把旧版本一直保留在联合类型里，就是读取旧数据所要付出的代价。

本包在此基础上多加了一步：旧的具体类型继续留在联合类型里，并且它们自己知道如何转换成最新版本，于是很久以前写下的数据依然可以被读出来，再在内存中完成升级。

### ILatestVersionConvertible\<T\>

联合类型实现 `ILatestVersionConvertible<T>`，其中 `T` 就是联合类型自身；联合类型里的每个具体类型都实现该接口的唯一成员：

```csharp
bool ConvertToLatestVersion(ref T value);
```

这个成员的约定是：

- 把 `value` 设置为升级后的实例，并返回 `true`——`value` 可以是同一个实例（就地修改）、同类型的另一个实例，或者更新的类型的实例。
- 返回 `false`——本实例已经是最新版本，`value` 保持原样。

该方法对每个值只会被调用 **一次**，所以把 `value` 设置成最新版本是实现方的责任。它可以一步到位，也可以委托给下一个版本的实现来接管：

```csharp
// 旧版本：把工作交给下一个版本
bool ILatestVersionConvertible<IMessage>.ConvertToLatestVersion(ref IMessage value)
{
    IMessage next = new MessageV2 { Text = Text, SentAt = default };
    next.ConvertToLatestVersion(ref next); // MessageV2 知道还有没有要做的事
    value = next;
    return true;
}
```

```csharp
using Aurora.MemoryPack;
using MemoryPack; // MemoryPackSerializer 与各项特性所在的命名空间

// 联合类型同时也就是 ILatestVersionConvertible<T> 的类型参数
[MemoryPackable]
[MemoryPackUnion(0, typeof(MessageV1))]
[MemoryPackUnion(1, typeof(MessageV2))]
public partial interface IMessage : ILatestVersionConvertible<IMessage>
{
}

// 旧版本：把自己转换成最新版本
[MemoryPackable]
public partial class MessageV1 : IMessage
{
    public string Text { get; set; } = string.Empty;

    bool ILatestVersionConvertible<IMessage>.ConvertToLatestVersion(ref IMessage value)
    {
        value = new MessageV2
        {
            Text = Text,
            SentAt = default
        };
        return true;
    }
}

// 最新版本：没有什么需要转换了
[MemoryPackable]
public partial class MessageV2 : IMessage
{
    public string Text { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; }

    bool ILatestVersionConvertible<IMessage>.ConvertToLatestVersion(ref IMessage value)
    {
        return false;
    }
}
```

不再被写入的类型，其联合标签也必须继续保留：只有确认所有可能包含它的存档数据都已重写（或者允许丢失）之后，才能把 `MessageV1` 从联合类型里删掉。

### `MemoryPackUtility.ConvertToLatestVersion`

`ConvertToLatestVersion` 会调用上述成员，并在确实发生了升级时向 `Aurora` 的日志写一行说明，其中包含新旧类型的联合标签。

```csharp
// 单个值：value 不能为 null
IMessage message = MemoryPackSerializer.Deserialize<IMessage>(bytes);
if (MemoryPackUtility.ConvertToLatestVersion(ref message))
{
    // 此时 message 是最新版本的实例
}

// 数组：逐元素就地升级
IMessage[] messages = MemoryPackSerializer.Deserialize<IMessage[]>(bytes);
if (MemoryPackUtility.ConvertToLatestVersion(messages))
{
    // 数组中至少有一个元素被升级了
}

// 列表：升级后的实例会通过索引器写回
var messageList = new List<IMessage> { message };
MemoryPackUtility.ConvertToLatestVersion(messageList);
```

| 重载                                     | 说明                                                                              |
|------------------------------------------|-----------------------------------------------------------------------------------|
| `ConvertToLatestVersion<T>(ref T value)` | `value` 不能为 `null`；返回 `value` 是否被升级                                    |
| `ConvertToLatestVersion<T>(T[] values)`  | 就地升级元素，跳过 `null` 元素；返回是否至少有一个元素被升级；`null` 返回 `false` |
| `ConvertToLatestVersion<T>(IList<T>)`    | 与数组版本相同，但会把升级后的实例写回列表                                        |

日志的样式举例：

```text
Instance (MessageV1, UnionTag = 0) has been upgraded to another instance (MessageV2, UnionTag = 1)
Instance (MessageV1, UnionTag = 0) has been upgraded
Instance (MessageV2, UnionTag = 1) has been upgraded to another instance of the same type
```

### `MemoryPackUtility.GetUnionTag`

从联合类型的 `[MemoryPackUnion]` 特性中读出某个具体类型的标签。反射结果会被缓存，因此重复查询同一个联合类型的标签开销很小。

```csharp
var unionTag = MemoryPackUtility.GetUnionTag(typeof(MessageV2), typeof(IMessage)); // 1
```

当联合类型完全没有添加 `[MemoryPackUnion]` 特性时抛出 `ArgumentException`；当目标类型不在其条目中时（例如已经从联合类型里删掉了某个标签，但仍在读取包含它的数据）抛出 `KeyNotFoundException`。

### `UnionInfo`

`UnionInfo` 是"联合类型 + 其中一项"的值形式：它把 `UnionType` 与 `UnionTargetTag`、`UnionTargetType` 组合在一起，并且可以比较、可以判等，因此可以排序，也可以放进集合里。

```csharp
var unionInfo = new UnionInfo(typeof(IMessage), 1, typeof(MessageV2));

var unionType = unionInfo.UnionType;             // typeof(IMessage)
var unionTag = unionInfo.UnionTargetTag;         // 1
var unionTargetType = unionInfo.UnionTargetType; // typeof(MessageV2)

var text = unionInfo.ToString(); // "IMessage @ [MemoryPackUnion(1, typeof(MessageV2))]"

var keys = new List<UnionInfo> { /* ... */ };
keys.Sort(); // IComparable<UnionInfo>：先按联合类型，再按标签，最后按目标类型
var isSameEntry = unionInfo.Equals(keys[0]); // IEquatable<UnionInfo>
```

## 深拷贝

`MemoryPackUtility.Clone` 通过一次 `MemoryPackSerializer.Serialize` 与 `MemoryPackSerializer.Deserialize` 往返来复制对象，从而得到深拷贝，不必手写任何克隆代码。

```csharp
[MemoryPackable]
public partial class PlayerData
{
    public int Level { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<int> Items { get; set; } = new();
}

var original = new PlayerData { Level = 10, Name = "Kevin" };
original.Items.Add(1);

var copy = MemoryPackUtility.Clone(original); // 深拷贝：copy.Items 是另一个列表

// 是集合也可以，只要 MemoryPack 能序列化它的元素
var originals = new List<PlayerData> { original };
var copies = MemoryPackUtility.Clone(originals);
```

`T` 可以是添加了 `[MemoryPackable]` 特性的类型，或者前述类型的集合（具体支持情况见 MemoryPack 文档）。参数按引用传递（`in T`），因此值类型不会被装箱。
