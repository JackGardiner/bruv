using static br.Br;

using FieldInfo = System.Reflection.FieldInfo;
using BindingFlags = System.Reflection.BindingFlags;
using MethodInfo = System.Reflection.MethodInfo;

namespace br {

// not so private to me.

public class PervField {

    public object obj;
    public FieldInfo field_info;

    public PervField(object obj, string name, bool is_static=false) {
        BindingFlags flags = BindingFlags.NonPublic;
        if (is_static)
            flags |= BindingFlags.Static;
        else
            flags |= BindingFlags.Instance;
        this.field_info = obj.GetType().GetField(name, flags)!;
        this.obj = obj;
    }

    public T get<T>() {
        return (T)field_info.GetValue(obj)!;
    }
    public void set(object? value) {
        field_info.SetValue(obj, value);
    }
}

public class PervMethod {
    public object obj;
    public MethodInfo method_info;

    public PervMethod(object obj, string name, bool is_static=false) {
        BindingFlags flags = BindingFlags.NonPublic;
        if (is_static)
            flags |= BindingFlags.Static;
        else
            flags |= BindingFlags.Instance;
        this.method_info = obj.GetType().GetMethod(name, flags)!;
        this.obj = obj;
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

public static class Perv {
    public static PervField field(object obj, string name,
            bool is_static=false) {
        return new(obj, name, is_static);
    }
    public static PervMethod method(object obj, string name,
            bool is_static=false) {
        return new(obj, name, is_static);
    }

    public static T get<T>(object obj, string name, bool is_static=false) {
        PervField f = new(obj, name, is_static);
        return f.get<T>();
    }
    public static void set(object obj, string name, object? value,
            bool is_static=false) {
        PervField f = new(obj, name, is_static);
        f.set(value);
    }

    public static void invoke(object obj, string name, bool is_static=false) {
        PervMethod m = new(obj, name, is_static);
        m.invoke();
    }
    public static T invoke<T>(object obj, string name, bool is_static=false) {
        PervMethod m = new(obj, name, is_static);
        return m.invoke<T>();
    }
    public static T invoke<T>(object obj, string name, bool is_static,
            params object?[]? args) {
        PervMethod m = new(obj, name, is_static);
        return m.invoke<T>(args);
    }
}

}
