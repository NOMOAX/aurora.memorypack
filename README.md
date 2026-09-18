# Aurora MemoryPack

![license](https://img.shields.io/github/license/NOMOAX/aurora.memorypack)
![version](https://img.shields.io/badge/version-1.0.3-blue)
![lowest Unity version](https://img.shields.io/badge/Unity-2021.2%2B-blue)

A companion toolkit for MemoryPack that adds version-migration support.

English | [中文](README.zh.md)

## Dependencies

- [Aurora](https://github.com/NOMOAX/aurora.git)
- [MemoryPack](https://github.com/Cysharp/MemoryPack)

## Installation

1. Open Unity package manager.
2. Click the `+` button in the upper-left corner, then select `Add package from git URL...`.
3. Input `https://github.com/NOMOAX/aurora.memorypack.git` and then click the `Add` button.

## Version Migration

MemoryPack serializes a union (an interface or an abstract class decorated with `[MemoryPackUnion]`) as the union tag of the concrete type plus that concrete type's members, which means the reader must still know every concrete type that may have been written before. Keeping the old versions forever is the price of reading old data.

This package adds one more step on top of that: the old concrete types stay in the union, and they know how to convert themselves into the newest version, so data written long ago can still be loaded and then upgraded in memory.

### ILatestVersionConvertible\<T\>

The union type implements `ILatestVersionConvertible<T>`, where `T` is the union type itself; every concrete type in the union implements the single member of that interface:

```csharp
bool ConvertToLatestVersion(ref T value);
```

The contract of that member is:

- Set `value` to the upgraded instance, and return `true` — `value` may be the same instance (mutated in place), another instance of the same type, or an instance of a newer type.
- Return `false` — this instance is already the latest version, and `value` is left as it is.

The method is called **once** per value, so it is the implementation's job to set `value` to the latest version. It may do so in one step, or it may delegate to the next version's implementation and let that one take over:

```csharp
// An old version: hands the work over to the next version
bool ILatestVersionConvertible<IMessage>.ConvertToLatestVersion(ref IMessage value)
{
    IMessage next = new MessageV2 { Text = Text, SentAt = default };
    next.ConvertToLatestVersion(ref next); // MessageV2 knows whether there is anything left to do
    value = next;
    return true;
}
```

```csharp
using Aurora.MemoryPack;
using MemoryPack; // the namespace of MemoryPackSerializer and the attributes

// The union type is also the type parameter of ILatestVersionConvertible<T>
[MemoryPackable]
[MemoryPackUnion(0, typeof(MessageV1))]
[MemoryPackUnion(1, typeof(MessageV2))]
public partial interface IMessage : ILatestVersionConvertible<IMessage>
{
}

// An old version: converts itself into the latest version
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

// The latest version: nothing left to convert
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

The union tag of a type that is no longer written still has to be reserved: dropping `MessageV1` from the union is possible only after every piece of saved data that may contain it has been rewritten (or is allowed to be lost).

### `MemoryPackUtility.ConvertToLatestVersion`

`ConvertToLatestVersion` calls that member and, when something was upgraded, writes a line to the `Aurora` log describing what happened, including the union tags of the old and the new type.

```csharp
// A single value: value must not be null
IMessage message = MemoryPackSerializer.Deserialize<IMessage>(bytes);
if (MemoryPackUtility.ConvertToLatestVersion(ref message))
{
    // message is now an instance of the latest version
}

// An array: upgraded in place, element by element
IMessage[] messages = MemoryPackSerializer.Deserialize<IMessage[]>(bytes);
if (MemoryPackUtility.ConvertToLatestVersion(messages))
{
    // The array contains at least one upgraded element
}

// A list: the upgraded instance is written back through the indexer
var messageList = new List<IMessage> { message };
MemoryPackUtility.ConvertToLatestVersion(messageList);
```

| Overload                                 | Notes                                                                                                                         |
|------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------|
| `ConvertToLatestVersion<T>(ref T value)` | `value` must not be `null`; returns whether `value` was upgraded                                                              |
| `ConvertToLatestVersion<T>(T[] values)`  | Upgrades the elements in place, skipping the `null` ones; returns whether at least one element was upgraded; `null` → `false` |
| `ConvertToLatestVersion<T>(IList<T>)`    | The same as the array overload, but the upgraded instance is assigned back into the list                                      |

An example of what the log looks like:

```text
Instance (MessageV1, UnionTag = 0) has been upgraded to another instance (MessageV2, UnionTag = 1)
Instance (MessageV1, UnionTag = 0) has been upgraded
Instance (MessageV2, UnionTag = 1) has been upgraded to another instance of the same type
```

### `MemoryPackUtility.GetUnionTag`

Reads the tag of a concrete type out of the `[MemoryPackUnion]` attributes of its union type. The reflection result is cached, so looking the tag of the same union type up again is cheap.

```csharp
var unionTag = MemoryPackUtility.GetUnionTag(typeof(MessageV2), typeof(IMessage)); // 1
```

It throws `ArgumentException` when the union type carries no `[MemoryPackUnion]` attribute at all, and `KeyNotFoundException` when the target type is not one of its entries (for example, when a tag was removed from the union but data containing it is still being read).

### `UnionInfo`

`UnionInfo` is the value form of "a union type plus one of its entries": it pairs `UnionType` with `UnionTargetTag` and `UnionTargetType`, and it can be compared and tested for equality, so it can be sorted or stored in a collection.

```csharp
var unionInfo = new UnionInfo(typeof(IMessage), 1, typeof(MessageV2));

var unionType = unionInfo.UnionType;             // typeof(IMessage)
var unionTag = unionInfo.UnionTargetTag;         // 1
var unionTargetType = unionInfo.UnionTargetType; // typeof(MessageV2)

var text = unionInfo.ToString(); // "IMessage @ [MemoryPackUnion(1, typeof(MessageV2))]"

var keys = new List<UnionInfo> { /* ... */ };
keys.Sort(); // IComparable<UnionInfo>: by union type, then by tag, then by target type
var isSameEntry = unionInfo.Equals(keys[0]); // IEquatable<UnionInfo>
```

## Deep Cloning

`MemoryPackUtility.Clone` copies an object through one round trip of `MemoryPackSerializer.Serialize` and `MemoryPackSerializer.Deserialize`, which gives a deep copy without writing any cloning code by hand.

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

var copy = MemoryPackUtility.Clone(original); // a deep copy: copy.Items is another list

// Being a collection is fine as well, as long as MemoryPack can serialize its elements
var originals = new List<PlayerData> { original };
var copies = MemoryPackUtility.Clone(originals);
```

`T` can be a type decorated with `[MemoryPackable]`, or a collection of the aforementioned types (see the MemoryPack documentation for the supported cases). The parameter is passed by reference (`in T`), so a value type is not boxed.
