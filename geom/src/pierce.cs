using static br.Br;

using FieldInfo = System.Reflection.FieldInfo;
using BindingFlags = System.Reflection.BindingFlags;
using MethodInfo = System.Reflection.MethodInfo;

namespace br {

// not so private to me.

public class PierceField {
    public object? obj { get; }
    public FieldInfo field_info { get; }

    public PierceField(object obj, string name) {
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        this.field_info = obj.GetType().GetField(name, flags)!;
        this.obj = obj;
    }
    public PierceField(Type type, string name) {
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        this.field_info = type.GetField(name, flags)!;
        this.obj = null;
    }

    public T? maybe_get<T>() {
        return (T?)field_info.GetValue(obj);
    }
    public T get<T>() {
        return (T)field_info.GetValue(obj)!;
    }
    public void set(object? value) {
        field_info.SetValue(obj, value);
    }
}

public class PierceMethod {
    public object? obj { get; }
    public MethodInfo method_info { get; }

    public PierceMethod(object obj, string name) {
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        this.method_info = obj.GetType().GetMethod(name, flags)!;
        this.obj = obj;
    }
    public PierceMethod(Type type, string name) {
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        this.method_info = type.GetMethod(name, flags)!;
        this.obj = null;
    }

    public void invoke() {
        _ = method_info.Invoke(obj, null);
    }
    public T invoke<T>() {
        return (T)method_info.Invoke(obj, null)!;
    }
    public T invoke<T>(params object?[]? args) {
        return (T)method_info.Invoke(obj, args)!;
    }
}

public class PierceNestedType {
    public Type nested_type { get; }

    public PierceNestedType(Type type, string name) {
        nested_type = type.GetNestedType(
            name,
            BindingFlags.NonPublic
        )!;
    }

    public object create(params object?[]? args) {
        return Activator.CreateInstance(
            nested_type,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: args,
            culture: null
        )!;
    }
}

public static class Pierce {
    public static PierceField field(object obj, string name) {
        return new(obj, name);
    }
    public static PierceField field(Type type, string name) {
        return new(type, name);
    }
    public static PierceMethod method(object obj, string name) {
        return new(obj, name);
    }
    public static PierceMethod method(Type type, string name) {
        return new(type, name);
    }
    public static PierceNestedType nested_type(Type type, string name) {
        return new(type, name);
    }

    public static T get<T>(object obj, string name) {
        PierceField f = new(obj, name);
        return f.get<T>();
    }
    public static T get<T>(Type type, string name) {
        PierceField f = new(type, name);
        return f.get<T>();
    }

    public static void set(object obj, string name, object? value) {
        PierceField f = new(obj, name);
        f.set(value);
    }
    public static void set(Type type, string name, object? value) {
        PierceField f = new(type, name);
        f.set(value);
    }

    public static void invoke(object obj, string name) {
        PierceMethod m = new(obj, name);
        m.invoke();
    }
    public static T invoke<T>(object obj, string name) {
        PierceMethod m = new(obj, name);
        return m.invoke<T>();
    }
    public static T invoke<T>(object obj, string name, params object?[]? args) {
        PierceMethod m = new(obj, name);
        return m.invoke<T>(args);
    }

    public static void invoke(Type type, string name) {
        PierceMethod m = new(type, name);
        m.invoke();
    }
    public static T invoke<T>(Type type, string name) {
        PierceMethod m = new(type, name);
        return m.invoke<T>();
    }
    public static T invoke<T>(Type type, string name, params object?[]? args) {
        PierceMethod m = new(type, name);
        return m.invoke<T>(args);
    }

    public static object create_nested_type(Type type, string name,
            params object?[]? args) {
        PierceNestedType nt = new(type, name);
        return nt.create(args);
    }
}

}
