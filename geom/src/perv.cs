using static br.Br;

using FieldInfo = System.Reflection.FieldInfo;
using BindingFlags = System.Reflection.BindingFlags;
using MethodInfo = System.Reflection.MethodInfo;

namespace br {

// not so private to me.

public class PervField {
    public object? obj { get; }
    public FieldInfo field_info { get; }

    public PervField(object obj, string name) {
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        this.field_info = obj.GetType().GetField(name, flags)!;
        this.obj = obj;
    }
    public PervField(Type type, string name) {
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

public class PervMethod {
    public object? obj { get; }
    public MethodInfo method_info { get; }

    public PervMethod(object obj, string name) {
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        this.method_info = obj.GetType().GetMethod(name, flags)!;
        this.obj = obj;
    }
    public PervMethod(Type type, string name) {
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

public class PervNestedType {
    public Type nested_type { get; }

    public PervNestedType(Type type, string name) {
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

public static class Perv {
    public static PervField field(object obj, string name) {
        return new(obj, name);
    }
    public static PervField field(Type type, string name) {
        return new(type, name);
    }
    public static PervMethod method(object obj, string name) {
        return new(obj, name);
    }
    public static PervMethod method(Type type, string name) {
        return new(type, name);
    }
    public static PervNestedType nested_type(Type type, string name) {
        return new(type, name);
    }

    public static T get<T>(object obj, string name) {
        PervField f = new(obj, name);
        return f.get<T>();
    }
    public static T get<T>(Type type, string name) {
        PervField f = new(type, name);
        return f.get<T>();
    }

    public static void set(object obj, string name, object? value) {
        PervField f = new(obj, name);
        f.set(value);
    }
    public static void set(Type type, string name, object? value) {
        PervField f = new(type, name);
        f.set(value);
    }

    public static void invoke(object obj, string name) {
        PervMethod m = new(obj, name);
        m.invoke();
    }
    public static T invoke<T>(object obj, string name) {
        PervMethod m = new(obj, name);
        return m.invoke<T>();
    }
    public static T invoke<T>(object obj, string name, params object?[]? args) {
        PervMethod m = new(obj, name);
        return m.invoke<T>(args);
    }

    public static void invoke(Type type, string name) {
        PervMethod m = new(type, name);
        m.invoke();
    }
    public static T invoke<T>(Type type, string name) {
        PervMethod m = new(type, name);
        return m.invoke<T>();
    }
    public static T invoke<T>(Type type, string name, params object?[]? args) {
        PervMethod m = new(type, name);
        return m.invoke<T>(args);
    }

    public static object create_nested_type(Type type, string name,
            params object?[]? args) {
        PervNestedType nt = new(type, name);
        return nt.create(args);
    }
}

}
