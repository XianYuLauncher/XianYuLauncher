using System.Collections;
using System.Linq;
using System.Text.Json;

namespace XianYuLauncher.Helpers;

internal sealed class PersistentObjectDictionary : IDictionary<string, object>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly Dictionary<string, object> _innerDictionary;
    private readonly object _syncRoot = new();

    private PersistentObjectDictionary(string filePath, Dictionary<string, object>? initialValues = null)
    {
        _filePath = filePath;
        _innerDictionary = initialValues ?? new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public static PersistentObjectDictionary Load(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? Path.GetTempPath());

        if (!File.Exists(filePath))
        {
            return new PersistentObjectDictionary(filePath);
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream);
            var values = document.RootElement.ValueKind == JsonValueKind.Object
                ? ConvertObject(document.RootElement)
                : new Dictionary<string, object>(StringComparer.Ordinal);
            return new PersistentObjectDictionary(filePath, values);
        }
        catch
        {
            return new PersistentObjectDictionary(filePath);
        }
    }

    public object this[string key]
    {
        get
        {
            lock (_syncRoot)
            {
                return _innerDictionary[key];
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _innerDictionary[key] = value;
                SaveCore();
            }
        }
    }

    public ICollection<string> Keys
    {
        get
        {
            lock (_syncRoot)
            {
                return _innerDictionary.Keys.ToArray();
            }
        }
    }

    public ICollection<object> Values
    {
        get
        {
            lock (_syncRoot)
            {
                return _innerDictionary.Values.ToArray();
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _innerDictionary.Count;
            }
        }
    }

    public bool IsReadOnly => false;

    public void Add(string key, object value)
    {
        lock (_syncRoot)
        {
            _innerDictionary.Add(key, value);
            SaveCore();
        }
    }

    public void Add(KeyValuePair<string, object> item)
    {
        Add(item.Key, item.Value);
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _innerDictionary.Clear();
            SaveCore();
        }
    }

    public bool Contains(KeyValuePair<string, object> item)
    {
        lock (_syncRoot)
        {
            return ((ICollection<KeyValuePair<string, object>>)_innerDictionary).Contains(item);
        }
    }

    public bool ContainsKey(string key)
    {
        lock (_syncRoot)
        {
            return _innerDictionary.ContainsKey(key);
        }
    }

    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
    {
        lock (_syncRoot)
        {
            ((ICollection<KeyValuePair<string, object>>)_innerDictionary).CopyTo(array, arrayIndex);
        }
    }

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        lock (_syncRoot)
        {
            return _innerDictionary.ToArray().AsEnumerable().GetEnumerator();
        }
    }

    public bool Remove(string key)
    {
        lock (_syncRoot)
        {
            var removed = _innerDictionary.Remove(key);
            if (removed)
            {
                SaveCore();
            }

            return removed;
        }
    }

    public bool Remove(KeyValuePair<string, object> item)
    {
        lock (_syncRoot)
        {
            var removed = ((ICollection<KeyValuePair<string, object>>)_innerDictionary).Remove(item);
            if (removed)
            {
                SaveCore();
            }

            return removed;
        }
    }

    public bool TryGetValue(string key, out object value)
    {
        lock (_syncRoot)
        {
            return _innerDictionary.TryGetValue(key, out value!);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private void SaveCore()
    {
        var serializableValues = NormalizeDictionary(_innerDictionary);
        var json = JsonSerializer.Serialize(serializableValues, SerializerOptions);
        File.WriteAllText(_filePath, json);
    }

    private static Dictionary<string, object> ConvertObject(JsonElement element)
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            values[property.Name] = ConvertValue(property.Value);
        }

        return values;
    }

    private static object ConvertValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertValue).ToList(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => string.Empty
        };
    }

    private static Dictionary<string, object?> NormalizeDictionary(IDictionary<string, object> dictionary)
    {
        var values = new Dictionary<string, object?>(dictionary.Count, StringComparer.Ordinal);
        foreach (var pair in dictionary)
        {
            values[pair.Key] = NormalizeValue(pair.Value);
        }

        return values;
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is IDictionary<string, object> dictionary)
        {
            return NormalizeDictionary(dictionary);
        }

        if (value is not string && value is IEnumerable sequence)
        {
            return sequence.Cast<object?>().Select(NormalizeValue).ToList();
        }

        return value;
    }
}