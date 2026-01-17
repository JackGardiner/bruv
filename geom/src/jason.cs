using static br.Br;

using JsonNode = System.Text.Json.Nodes.JsonNode;
using JsonMap = System.Text.Json.Nodes.JsonObject; // object is a stupid name.
using JsonList = System.Text.Json.Nodes.JsonArray;
using JsonValue = System.Text.Json.Nodes.JsonValue;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace br {

public class Jason {
    protected JsonMap _root;

    protected static JsonMap _parse(string path) {
        string text = File.ReadAllText(path);
        JsonNode node = JsonNode.Parse(text)
            ?? throw new Exception($"invalid JSON, at: '{path}'");
        if (node is not JsonMap map)
            throw new Exception($"non-map root, at: '{path}'");
        return map;
    }

    protected static void _merge(JsonMap dst, JsonMap src) {
        foreach (var kvp in src) {
            string key = kvp.Key;
            JsonNode nodesrc = kvp.Value!;
            if (dst.TryGetPropertyValue(key, out JsonNode? nodedst)) {
                if (nodesrc is JsonMap mapsrc && nodedst is JsonMap mapdst) {
                    _merge(mapdst, mapsrc);
                } else {
                    dst[key] = nodesrc.DeepClone();
                }
            } else {
                dst[key] = nodesrc.DeepClone();
            }
        }
    }

    protected static bool _is_list(Type type, out Type elemtype) {
        elemtype = typeof(Jason);
        if (!type.IsGenericType)
            return false;
        if (type.GetGenericTypeDefinition() != typeof(List<>))
            return false;
        elemtype = type.GetGenericArguments()[0];
        return true;
    }

    protected JsonMap _parent_leaf(string name, out string leaf) {
        string[] parts = name.Split("/", StringSplitOptions.RemoveEmptyEntries);
        if (numel(parts) == 0)
            throw new Exception($"empty name: '{name}'");
        JsonMap parent = _root;
        for (int i=0; i<numel(parts) - 1; ++i) {
            string key = parts[i];
            if (!parent.TryGetPropertyValue(key, out JsonNode? node))
                throw new Exception($"missing segment '{key}', of: '{name}'");
            if (node is not JsonMap obj)
                throw new Exception($"non-map segment '{key}', of '{name}'");
            parent = obj;
        }
        leaf = parts[^1];
        return parent;
    }


    public Jason(string path) {
        _root = _parse(path);
    }

    public void overwrite_from(string path) {
        JsonMap newmap = _parse(path);
        _merge(_root, newmap);
    }

    public T get<T>(string name) {
        Type type = typeof(T);

        JsonMap parent = _parent_leaf(name, out string leaf);
        if (!parent.TryGetPropertyValue(leaf, out JsonNode? node))
            throw new Exception($"leaf doesn't exist, of: '{name}'");

        if (_is_list(type, out Type elemtype)) {
            if (node is not JsonList jsonlist)
                throw new Exception($"leaf is not an array, of: '{name}'");

            Type listtype = typeof(List<>).MakeGenericType(elemtype);
            var list = (System.Collections.IList)
                       Activator.CreateInstance(listtype)!;

            foreach (JsonNode? item in jsonlist) {
                if (node is not JsonValue value)
                    throw new Exception($"list element is not a value, of: "
                            + $"'{name}'");
                if (!value.TryGetValue(out T? result))
                    throw new Exception($"list element is not of type "
                            + $"{typeof(T).Name}, of: '{name}'");
                list.Add(result!);
            }
            return (T)list!;
        } else if (type == typeof(JsonMap)) {
            if (node is not JsonMap map)
                throw new Exception($"leaf is not a map, of: '{name}'");
            return (T)(object)map;
        } else {
            if (node is not JsonValue value)
                throw new Exception($"leaf is not a value, of: '{name}'");
            if (!value.TryGetValue(out T? result))
                throw new Exception($"leaf is not of type {typeof(T).Name}, of: "
                        + $"'{name}'");
            return result!;
        }
    }
    public JsonMap get_map(string name) => get<JsonMap>(name);

    public void set<T>(string name, in T value) {
        Type type = typeof(T);

        JsonMap parent = _parent_leaf(name, out string leaf);
        if (parent.ContainsKey(leaf))
            throw new Exception($"leaf already exists, of: '{name}'");

        if (_is_list(type, out _)) {
            if (value is not System.Collections.IEnumerable enumerable)
                throw new Exception();
            JsonList list = new();
            foreach (var item in enumerable) {
                assert(item != null);
                JsonNode? node = JsonValue.Create(item);
                list.Add(node);
            }
            parent[leaf] = list;
        } else if (type == typeof(JsonMap)) {
            if (value is not JsonMap map)
                throw new Exception();
            parent[leaf] = map.DeepClone();
        } else {
            parent[leaf] = JsonValue.Create(value);
        }
    }
    public void new_map(string name) {
        JsonMap parent = _parent_leaf(name, out string leaf);
        if (parent.ContainsKey(leaf))
            throw new Exception($"leaf already exists, of: '{name}'");

        parent[leaf] = new JsonMap();
    }

    public T deserialise<T>(string name) {
        JsonMap parent = _parent_leaf(name, out string leaf);
        if (!parent.TryGetPropertyValue(leaf, out JsonNode? node))
            throw new Exception($"leaf doesn't exist, of: '{name}'");
        if (node is not JsonMap map)
            throw new Exception($"leaf is not a map, of: '{name}'");
        return JsonSerializer.Deserialize<T>(map)
            ?? throw new Exception($"failed to deserialise '{name}'");
    }
}

}
